using SpatialEnrichment;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SpatialEnrichmentWrapper
{
    public class VolatileTesselation
    {
        private List<Tuple<ICoordinate, bool>> Data;
        private List<Hyperplane> Bisectors;
        private int Dimensionality;
        private static int[][] CoordIdsMap;

        public VolatileTesselation(List<Tuple<ICoordinate, bool>> data)
        {
            Dimensionality = data.First().Item1.GetDimensionality();
            Data = data;
            Bisectors = new List<Hyperplane>();
            switch (Dimensionality)
            {
                case 2:
                    Line.InitNumPoints(data.Count);
                    break;
                case 3:
                    Plane.InitNumPoints(data.Count);
                    break;
            }

            for (var i = 0; i < data.Count; i++)
                for (var j = i + 1; j < data.Count; j++)
                    if (data[i].Item2 != data[j].Item2)
                    {
                        Hyperplane line = null;
                        switch (Dimensionality)
                        {
                            case 2:
                                line = Line.Bisector((Coordinate)data[i].Item1, (Coordinate)data[j].Item1);
                                break;
                            case 3:
                                line = Plane.Bisector((Coordinate3D)data[i].Item1, (Coordinate3D)data[j].Item1);
                                break;
                        }
                        line.SetPointIds(i, j);
                        Bisectors.Add(line);
                    }
            CoordIdsMap = Dimensionality == 2 ? Line.LineIdsMap : Plane.PlaneIdsMap;
        }

        private int GetBisectorId(int first, int second)
        {
            if (first < second)
                return CoordIdsMap[first][second];
            else
                return CoordIdsMap[second][first];
        }

        public Cell PointToCell(ICoordinate point)
        {
            //Order points by distance from pivot
            var sortedDistances = Data.AsParallel()
                .Select((p, idx) => new { Point = p.Item1, Label = p.Item2, PointId = idx, Distance = p.Item1.EuclideanDistance(point) })
                .OrderBy(t => t.Distance).ToArray();

            //Only bisectors that impact the order of elements in the binary vector may participate in the cell boundaries.
            //Since crossing a bisector may only swap 2 adjacent elements in the vector, and since order in each group makes no difference -
            //these are bisectors seperating consecutive groups of 1's and 0's.
            var relevantLines = new List<Hyperplane>();
            for (var i = 0; i < sortedDistances.Length - 1;)
            {
                var consecGroup1 = sortedDistances.Skip(i).TakeWhile(p => p.Label == sortedDistances[i].Label).ToList(); //maps to identically significant neighbors
                var consecGroup2 = sortedDistances.Skip(i + consecGroup1.Count).TakeWhile(p => p.Label == !sortedDistances[i].Label).ToList();
                i = i + consecGroup1.Count;
                relevantLines.AddRange(from pt1 in consecGroup1
                                       from pt2 in consecGroup2
                                       select Bisectors[GetBisectorId(pt1.PointId, pt2.PointId)]);
            }

            var OrderedLines =
                relevantLines.AsParallel()
                .Select(l => new { Line = l, Distance = l.DistanceToPoint(point) })
                .OrderBy(l => l.Distance)
                .ToList();

            var closestLine = OrderedLines.First().Line;
            var startCoord = closestLine.ProjectOnto(point); //this is a coordinate on the closest interval 

            var cm = new CoordMesh(relevantLines);
            var startSeg = cm.GetSegmentContainingCoordinateOnLine(closestLine.Id, startCoord);

            Cell prevCell = null;
            var linecount = 0;
            if (startSeg != null)
            {
                var segAngle = LineSegment.GetAngle(coord, startCoord, startCoord, startSeg.SecondIntersectionCoord);
                var direction = startSeg.IsPositiveLexicographicProgress() ^ (segAngle > 180) ? SegmentCellCovered.Right : SegmentCellCovered.Left;
                prevCell = CoverCellFromSegment(cm, startSeg, direction);
            }

            return prevCell;

            return null;
        }


    }
}
