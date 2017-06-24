using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using System.Security.Claims;
using System.Security.Policy;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SpatialEnrichment
{
    public class CoordMesh
    {
        private List<Line> Lines;
        public List<Coordinate>[] LineToCoords;
        private Coordinate[,] LineIntersections; //maps a coordinate to its 4 (max) neighbors on the mesh
        private bool[,] CoveredIntersectionsRight, CoveredIntersectionsLeft;
        private Coordinate[] coords; //maps coordid to coord
        public long numCoords;
        public long segmentCount=0;
        // Each row captures for vertex v the following order of relations: [pos x progress & right angle,pos x progress & left angle,neg x progress & right angle,neg x progress & left angle]
        // aka [+R,+L,-R,-L]
        private int Nlines;
        public CoordMesh(List<Line> lines)
        {
            var sw = Stopwatch.StartNew();
            Console.Write("Building mesh... ");
            LineToCoords = new List<Coordinate>[lines.Count];
            Lines = lines;
            Nlines = lines.Count;
            l1id = new List<int>(Nlines);
            for (var i = 0; i < Nlines; i++)
                l1id.Add(0);
            for (var i = 1; i < Nlines; i++)
            {
                l1id[i] = l1id[i - 1] + Nlines - i;
            }
            numCoords = (long)lines.Count * (lines.Count - 1) / 2;
            coords = new Coordinate[numCoords];
            LineIntersections = new Coordinate[numCoords, 4];
            CoveredIntersectionsRight = new bool[numCoords, 4];
            CoveredIntersectionsLeft = new bool[numCoords, 4];

            for (var i = 0; i < lines.Count; i++)
            {
                var ltr = lines.Where(l => l.Id != lines[i].Id)
                    .AsParallel()
                    .Select(l => new {Line = l, Coord = l.Intersection(lines[i]) })
                    .OrderBy(t => t.Coord.X).ToList();
                LineToCoords[i] = new List<Coordinate>(ltr.Select(t=>t.Coord));
                for (var j = 0; j < ltr.Count; j++)
                {
                    var inVertexId  = CoordIdFromLineIds(lines[i].Id, ltr[j].Line.Id);
                    ltr[j].Coord.CoordId = inVertexId;
                    coords[inVertexId] = ltr[j].Coord;
                    var isfirst = LineIntersections[inVertexId , 0] == null &&
                                  LineIntersections[inVertexId , 1] == null
                        ? 0
                        : 2;

                    if (j > 0)
                    {
                        LineIntersections[inVertexId, isfirst + 0] = ltr[j - 1].Coord;
                    }
                    if (j < ltr.Count - 1)
                    {
                        LineIntersections[inVertexId, isfirst + 1] = ltr[j + 1].Coord;
                    }
                }
            }
            Console.WriteLine("Done. {0}s elapsed.", sw.ElapsedMilliseconds / 1000);
        }
        private List<int> l1id;
        public int CoordIdFromLineIds(int line1, int line2)
        {
            var linea = Math.Min(line1, line2);
            var lineb = Math.Max(line1, line2);
            return l1id[linea] + (lineb - linea) - 1;
        }

        private int LineIdFromCoordIdExceptIdx(int cid, int lid)
        {
            var tres = LineIdsFromCoordId(cid);
            if (tres.Item1 == lid) return tres.Item2;
            else return tres.Item1;
        }

        public Tuple<int,int> LineIdsFromCoordId(int cid)
        {
            //int i = l1id.BinarySearch(cid);
            //i = i < 0 ? ~i: i;
            var i = SolveToFindIndex(cid);
            while (l1id[i] > cid) i--;
            while (l1id[i] < cid) i++;
            if(l1id[i] > cid)
                i--;
            var l1 = i;
            var l2 = l1 + 1 + cid - l1id[l1];
            if (CoordIdFromLineIds(l1,l2) != cid)
                Console.Write('.');
            return new Tuple<int, int>(l1, l2);
        }


        /// <summary>
        /// Uses the fact that Sum_{k=0}^y [N-y] = (2N-1)
        /// </summary>
        /// <param name="X"></param>
        /// <returns></returns>
        public int SolveToFindIndex(int X)
        {
            var a = -0.5;
            var b = Nlines - 0.5;
            var c = -X;
            var sol = QuadraticEquationSolver(a, b, c);
            var res = sol.Item2; //(sol.Item1 < Nlines) ? sol.Item1 : sol.Item2;
            return (int)res;
            //return (int) Math.Max(plusSol, minusSol);
        }

        public static Tuple<double, double> QuadraticEquationSolver(double a, double b, double c)
        {
            var x1 = (-b - Math.Sign(b) * Math.Sqrt(b * b - 4 * a * c)) / (2 * a);
            var x2 = c / (a * x1);
            return new Tuple<double, double>(x1,x2);
        }


        public Tuple<IEnumerable<Line>, IEnumerable<Line>> GetAllLinesAroundSegment(LineSegment seg)
        {
            Coordinate leftCoord, rightCoord;
            if(seg.FirstIntersectionCoord.X < seg.SecondIntersectionCoord.X)
            {
                leftCoord = seg.FirstIntersectionCoord;
                rightCoord = seg.SecondIntersectionCoord;
            }
            else
            {
                leftCoord = seg.SecondIntersectionCoord;
                rightCoord = seg.FirstIntersectionCoord;
            }
            var firstIdx = LineToCoords[seg.Source.Id].BinarySearch(leftCoord, new CoordinateComparer());
            var secondIdx = firstIdx + 1;

            /*
            var leftList = new List<Line>(firstIdx+1);
            var rightList = new List<Line>(LineToCoords[seg.Source.Id].Count - firstIdx - 1);
            for (var i= firstIdx + 1; i>=0; i--)
            {
                var c = LineToCoords[seg.Source.Id][i];
                leftList.Add(Lines[LineIdFromCoordIdExceptIdx(c.CoordId.Value, seg.Source.Id)]);
            }
            for (var i=secondIdx; i< LineToCoords[seg.Source.Id].Count; i++)
            {
                var c = LineToCoords[seg.Source.Id][i];
                rightList.Add(Lines[LineIdFromCoordIdExceptIdx(c.CoordId.Value, seg.Source.Id)]);
            }
            */
            var leftCoords = LineToCoords[seg.Source.Id].Take(firstIdx + 1).Reverse();
            var rightCoords = LineToCoords[seg.Source.Id].Skip(secondIdx);
            var leftList = leftCoords
                .Select(c => Lines[LineIdFromCoordIdExceptIdx(c.CoordId.Value, seg.Source.Id)]);
            var rightList = rightCoords
                .Select(c => Lines[LineIdFromCoordIdExceptIdx(c.CoordId.Value, seg.Source.Id)]);
            return new Tuple<IEnumerable<Line>, IEnumerable<Line>>(leftList, rightList);
        }

        public LineSegment GetSegmentContainingCoordinateOnLine(int closestLineId, Coordinate startCoord)
        {
            var nearestHit = LineToCoords[closestLineId].BinarySearch(startCoord, new CoordinateComparer());
            nearestHit = nearestHit < 0 ? ~nearestHit : nearestHit;
            if (nearestHit == 0 || nearestHit > Nlines-2) return null;
            var tmp1 = LineToCoords[closestLineId][nearestHit-1];
            var tmp2 = LineToCoords[closestLineId][nearestHit];
            var c1lines = LineIdsFromCoordId(tmp1.CoordId.Value);
            var c2lines = LineIdsFromCoordId(tmp2.CoordId.Value);
            var c1l = c1lines.Item1 != closestLineId ? c1lines.Item1 : c1lines.Item2;
            var c2l = c2lines.Item1 != closestLineId ? c2lines.Item1 : c2lines.Item2;
            var ls = new LineSegment(Lines[closestLineId], Lines[c1l], Lines[c2l], tmp1, tmp2);
            return ls;
        }

        public IEnumerable<LineSegment> GetSegmentNeighbors(LineSegment prevSeg, Tesselation.SegmentCellCovered nextDirection)
        {
            var inVertexId = prevSeg.SecondIntersectionCoord.CoordId.Value;
            var relevantLines = LineIdsFromCoordId(inVertexId);
            var newSource = relevantLines.Item1 != prevSeg.Source.Id ? relevantLines.Item1 : relevantLines.Item2;
            var inLexProg = prevSeg.IsPositiveLexicographicProgress() ? 0 : 2;
            var neighborCoords = VertexNeighbors(inVertexId)
               .Select((v, id) => new { Vertex = v, Id = id }).Where(v => v.Vertex != null && v.Vertex.CoordId != prevSeg.FirstIntersectionCoord.CoordId)
               .Select(v => new
               {
                   v.Vertex,
                   v.Id,
                   Angle = LineSegment.GetAngle(prevSeg.FirstIntersectionCoord, prevSeg.SecondIntersectionCoord,
                                                prevSeg.SecondIntersectionCoord, v.Vertex),
                   LineIds = LineIdsFromCoordId(v.Vertex.CoordId.Value)
               }).ToList();
            if ((nextDirection & Tesselation.SegmentCellCovered.Right) != 0)
            {
                var nextCoord = neighborCoords.FirstOrDefault(v => v.Angle < 180 && v.LineIds.Item1 != prevSeg.Source.Id && v.LineIds.Item2 != prevSeg.Source.Id);
                if (nextCoord != null)
                {
                    var coordlineid = nextCoord.LineIds.Item1 != newSource ? nextCoord.LineIds.Item1 : nextCoord.LineIds.Item2;
                    yield return new LineSegment(Lines[newSource], prevSeg.SecondIntersection, Lines[coordlineid], prevSeg.SecondIntersectionCoord, nextCoord.Vertex);
                }
            }
            if ((nextDirection & Tesselation.SegmentCellCovered.Left) != 0)
            {
                var nextCoord = neighborCoords.FirstOrDefault(v => v.Angle > 180 && v.LineIds.Item1 != prevSeg.Source.Id && v.LineIds.Item2 != prevSeg.Source.Id);
                if (nextCoord != null)
                {
                    var coordlineid = nextCoord.LineIds.Item1 != newSource ? nextCoord.LineIds.Item1 : nextCoord.LineIds.Item2;
                    yield return new LineSegment(Lines[newSource], prevSeg.SecondIntersection, Lines[coordlineid], prevSeg.SecondIntersectionCoord, nextCoord.Vertex);
                }
            }
        }

        public IEnumerable<Coordinate> VertexNeighbors(int vid)
        {
            for (var i = 0; i< 4; i++)
                yield return LineIntersections[vid, i];
        }

        public bool WasCovered(LineSegment nextSeg, Tesselation.SegmentCellCovered nextDirection)
        {
            var from = nextSeg.FirstIntersectionCoord.CoordId.Value;
            var to = nextSeg.SecondIntersectionCoord.CoordId.Value;
            var toNeighId = GetCoordNeighborId(from, to);

            if ((nextDirection & Tesselation.SegmentCellCovered.Right) != 0)
            {
                return CoveredIntersectionsRight[from, toNeighId];
            }
            if ((nextDirection & Tesselation.SegmentCellCovered.Left) != 0)
            {
                return CoveredIntersectionsLeft[from, toNeighId];
            }
            return false;
            /*
            var inVertexId = nextSeg.SecondIntersectionCoord.CoordId.Value;
            var inLexProg = nextSeg.IsPositiveLexicographicProgress();
            var covered = true;
            var neighborCoords = VertexNeighbors(inVertexId)
               .Select((v, id) => new { Vertex = v, Id = id }).Where(v=>v.Vertex!=null).Select(v=> new
               {
                   v.Vertex,
                   v.Id,
                   Angle = LineSegment.GetAngle(nextSeg.FirstIntersectionCoord, nextSeg.SecondIntersectionCoord,
                                            nextSeg.SecondIntersectionCoord, v.Vertex),
                   LineIds = LineIdsFromCoordId(v.Vertex.CoordId.Value)
               }).ToList();

            if ((nextDirection & Tesselation.SegmentCellCovered.Right) != 0)
            {
                var nextCoord = neighborCoords.FirstOrDefault(v=> inLexProg ? v.Angle < 180 : v.Angle > 180 && v.LineIds.Item1 != nextSeg.Source.Id && v.LineIds.Item2 != nextSeg.Source.Id);
                if(nextCoord != null)
                    covered = covered &  this.CoveredIntersectionsRight[inVertexId, nextCoord.Id];
            }
            if ((nextDirection & Tesselation.SegmentCellCovered.Left) != 0)
            {
                var nextCoord = neighborCoords.FirstOrDefault(v => inLexProg ? v.Angle > 180 : v.Angle < 180 && v.LineIds.Item1 != nextSeg.Source.Id && v.LineIds.Item2 != nextSeg.Source.Id);
                if (nextCoord != null)
                    covered = covered & this.CoveredIntersectionsLeft[inVertexId, nextCoord.Id];
            }
            return covered;
            */
        }

        private int GetCoordNeighborId(int fromId, int toId)
        {
            return VertexNeighbors(fromId)
                    .Select((v, id) => new { v, id }).Where(t => t.v != null)
                    .First(t => t.v.CoordId == toId)
                    .id;
        }


        public void CoverCoordPair(Coordinate from, Coordinate to, Tesselation.SegmentCellCovered direction)
        {
            Interlocked.Increment(ref segmentCount);
            var fromid = GetCoordNeighborId(from.CoordId.Value, to.CoordId.Value);
            var toid = GetCoordNeighborId(to.CoordId.Value, from.CoordId.Value);
            if ((direction & Tesselation.SegmentCellCovered.Right) != 0)
            {
                this.CoveredIntersectionsRight[from.CoordId.Value, fromid] = true;
                this.CoveredIntersectionsLeft[to.CoordId.Value, toid] = true;
            }
            if ((direction & Tesselation.SegmentCellCovered.Left) != 0)
            {
                this.CoveredIntersectionsLeft[from.CoordId.Value, fromid] = true;
                this.CoveredIntersectionsRight[to.CoordId.Value, toid] = true;
            }
        }

        /// <summary>
        /// Cover the points in the segment from the segment's 'direction' side.
        /// </summary>
        /// <param name="seg"></param>
        /// <param name="direction"></param>
        public void CoverSegment(LineSegment seg, Tesselation.SegmentCellCovered direction)
        {
            //CoverCoordPair(seg.FirstIntersectionCoord,seg.SecondIntersectionCoord, direction);
            var inVertexId = seg.SecondIntersectionCoord.CoordId.Value;
            var inLexProg = seg.IsPositiveLexicographicProgress();
            var covered = true;
            var neighborCoords = VertexNeighbors(inVertexId)
                           .Select((v, id) => new { Vertex = v, Id = id }).Where(v => v.Vertex != null).Select(v => new
                           {
                               v.Vertex,
                               v.Id,
                               Angle = LineSegment.GetAngle(seg.FirstIntersectionCoord, seg.SecondIntersectionCoord,
                                                            seg.SecondIntersectionCoord, v.Vertex),
                               LineIds = LineIdsFromCoordId(v.Vertex.CoordId.Value)
                           }).ToList();
            if ((direction & Tesselation.SegmentCellCovered.Right) != 0)
            {
                var nextCoord = neighborCoords.FirstOrDefault(v => inLexProg ? v.Angle < 180 : v.Angle > 180 && v.LineIds.Item1 != seg.Source.Id && v.LineIds.Item2 != seg.Source.Id);
                if(nextCoord!=null)
                    CoverCoordPair(seg.SecondIntersectionCoord, nextCoord.Vertex, direction);
            }
            if ((direction & Tesselation.SegmentCellCovered.Left) != 0)
            {
                var nextCoord = neighborCoords.FirstOrDefault(v => inLexProg ? v.Angle > 180 : v.Angle < 180 && v.LineIds.Item1 != seg.Source.Id && v.LineIds.Item2 != seg.Source.Id);
                if (nextCoord != null)
                    CoverCoordPair(seg.SecondIntersectionCoord, nextCoord.Vertex, direction);
            }
        }

        public LineSegment GetSegment(int sourceId, Line lineLine, Line lastLine)
        {
            var firstIntersection = coords[CoordIdFromLineIds(sourceId, lineLine.Id)];
            var secondIntersection = coords[CoordIdFromLineIds(sourceId, lastLine.Id)];
            if (!VertexNeighbors(firstIntersection.CoordId.Value).Contains(secondIntersection))
                Console.WriteLine('.');
            return new LineSegment(Lines[sourceId], lineLine, lastLine, firstIntersection, secondIntersection);
        }

        public IEnumerable<Tuple<LineSegment,Tesselation.SegmentCellCovered>> GetUncoveredSegments()
        {
            for(var cid=0; cid< numCoords; cid++)
                for (var neighid=0;neighid<4;neighid++)
                    if ((!CoveredIntersectionsRight[cid, neighid] ||
                         !CoveredIntersectionsLeft[cid, neighid]) && LineIntersections[cid, neighid] != null)
                    {
                        var c2id = LineIntersections[cid, neighid].CoordId.Value;
                        var C1lines = LineIdsFromCoordId(cid);
                        var C2lines = LineIdsFromCoordId(c2id);
                        int sourceId = -1;
                        if (C1lines.Item1 == C2lines.Item1)
                        {
                            sourceId = C1lines.Item1;
                        }
                        if (C1lines.Item1 == C2lines.Item2)
                        {
                            sourceId = C1lines.Item1;
                        }
                        if (C1lines.Item2 == C2lines.Item1)
                        {
                            sourceId = C1lines.Item2;
                        }
                        if (C1lines.Item2 == C2lines.Item2)
                        {
                            sourceId = C1lines.Item2;
                        }
                        if(!CoveredIntersectionsRight[cid, neighid])
                        yield return
                            new Tuple<LineSegment, Tesselation.SegmentCellCovered>(
                                new LineSegment(Lines[sourceId], Lines[LineIdFromCoordIdExceptIdx(cid, sourceId)],
                                    Lines[LineIdFromCoordIdExceptIdx(c2id, sourceId)],
                                    coords[cid], coords[c2id]), Tesselation.SegmentCellCovered.Right);
                        if (!CoveredIntersectionsLeft[cid, neighid])
                            yield return
                            new Tuple<LineSegment, Tesselation.SegmentCellCovered>(
                                new LineSegment(Lines[sourceId], Lines[LineIdFromCoordIdExceptIdx(cid, sourceId)],
                                    Lines[LineIdFromCoordIdExceptIdx(c2id, sourceId)],
                                    coords[cid], coords[c2id]), Tesselation.SegmentCellCovered.Left);
                    }
        }
    }
}
