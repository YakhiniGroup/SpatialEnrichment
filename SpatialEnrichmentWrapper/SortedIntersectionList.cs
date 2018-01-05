using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SpatialEnrichmentWrapper;

namespace SpatialEnrichment
{
    public class SortedIntersectionData : IEnumerable<SortedIntersectionList>
    {
        private SortedIntersectionList[] sil;
        private ConcurrentDictionary<int, Line> obsLines = new ConcurrentDictionary<int, Line>();
        public SortedIntersectionList this[int key] {  get { return GetValue(key); } set { SetValue(key, value); } }
        private object locker = new object();

        public SortedIntersectionData(int count)
        {
            sil = new SortedIntersectionList[count];
        }

        private void SetValue(int key, SortedIntersectionList value)  { sil[key] = value; }
        private SortedIntersectionList GetValue(int key)  { return sil[key]; }

        public Coordinate FirstIntersectionCoord(LineSegment ls)
        {
            return sil[ls.Source.Id].LineToCoordinate[ls.FirstIntersection];
        }
        public Coordinate SecondIntersectionCoord(LineSegment ls)
        {
            return sil[ls.Source.Id].LineToCoordinate[ls.SecondIntersection];
        }

        public IEnumerator<SortedIntersectionList> GetEnumerator()
        {
            return sil.AsEnumerable().GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public void ResetVisitationData()
        {
            foreach(var item in sil.Where(s=>s!=null))
                item.ResetVisitationData();
        }

        public void AddLine(Line cellline, List<Line> lineList)
        {
            lock (locker)
            {
                var newLines = lineList.Where(l => l != null && !obsLines.ContainsKey(l.Id)).ToList();
                foreach (var l in newLines)
                    obsLines.GetOrAdd(l.Id, l);
                Parallel.ForEach(sil.Where(ls => ls != null), ls =>
                {
                    foreach (var l in newLines)
                        if (ls.MyLine.Id != l.Id)
                            ls.AddLineToExistingData(l, this);
                });

                //recover previously obs lines
                var tlineList = lineList.ToList();
                foreach (var l in obsLines)
                    if (tlineList[l.Key] == null)
                        tlineList[l.Key] = l.Value;
                //genereate a new SortedIntersectionLists
                if (sil[cellline.Id] == null)
                {
                    sil[cellline.Id] = new SortedIntersectionList(cellline.Id, tlineList);
                    sil[cellline.Id].PopulateSegmentData(cellline, this);
                }

                //add line to existing SortedIntersectionLists
                Parallel.ForEach(sil.Where(ls => ls != null && ls.MyLine.Id != cellline.Id), ls =>
                {
                    if (!ls.ContainsLine(cellline))
                        ls.AddLineToExistingData(cellline, this);
                });
            }
        }

        /// <summary>
        /// Gets the correct neighbor for a segment
        /// </summary>
        /// <param name="seg"></param>
        /// <param name="dir"></param>
        /// <returns></returns>
        public IEnumerable<LineSegment> GetSegmentNeighbors(LineSegment inSeg, Tesselation.SegmentCellCovered dir = Tesselation.SegmentCellCovered.Both)
        {
            var posLexProg = inSeg.IsPositiveLexicographicProgress();
            //get neighboring segments
            var adjSegments = this[inSeg.SecondIntersection.Id].GetSegmentsFromLine(inSeg.Source)
                .Where(s => s != null)
                //find correct lexicographic order
                .Select(s=> inSeg.Source.Id == s.FirstIntersection.Id ? 
                        s : this[inSeg.SecondIntersection.Id].GetSegmentInverse(s))
                .ToList();
            foreach(var seg in adjSegments)
            {
                var posAngle = inSeg.IsPositiveAngle(seg);
                if (posLexProg)
                {
                    if (posAngle ^ (dir & Tesselation.SegmentCellCovered.Right) != 0)
                        yield return seg;
                }
                else
                    if (!posAngle ^ (dir & Tesselation.SegmentCellCovered.Left) != 0)
                    yield return seg;
            }
        }
}

    public class SortedIntersectionList
    {
        public Line[] LineList; //holds all lines sorted by the location they intersect with this line
        //private int[] ranks; //for entry ranks[line.id] is its rank in the aggregated sorted list
        private Dictionary<int, int> ranks;
        public ConcurrentDictionary<Line, Coordinate> LineToCoordinate;
        public Dictionary<Coordinate, HashSet<Line>> CoordinateToLine; //all lines that intersect in the same coordinate (within precision)
        public LineSegment[] Segments;
        private LineSegment[] _segmentsInv;
        private bool[] LeftCellsVisited, RightCellsVisited;
        private Cell[] SegmentToCellMap; //this is sparse, we hold it as a list.
        private Object locker = new object();
        private int count = 0;

        public int MyId { get; private set; }
        public Line MyLine;

        public SortedIntersectionList(int lineId, List<Line> lines)
        {
            //ranks = new int[Line.Count];
            ranks = new Dictionary<int, int>();
            LineToCoordinate = new ConcurrentDictionary<Line, Coordinate>();
            CoordinateToLine = new Dictionary<Coordinate, HashSet<Line>>();

            MyLine = lines.First(l=>l!=null && l.Id == lineId);
            MyId = lineId;
            var intersectionlst = lines.Select((v, j) => new { line = v, idx = j })
                .Where(v => v.line != null && v.line.Id != lineId)
                .Select(l => new { l.line, l.idx, intersection = MyLine.Intersection(l.line) })
                .GroupBy(l => l.intersection)
                .OrderBy(g => g.Key.X)
                .ThenBy(g => g.Key.Y);
            
            count = 0;
            var tmpLinelist = new List<Line>();
            foreach (var g in intersectionlst)
            {
                CoordinateToLine.Add(g.Key, new HashSet<Line>());
                foreach (var lineset in g)
                {
                    LineToCoordinate.GetOrAdd(lineset.line, g.Key);
                    CoordinateToLine[g.Key].Add(lineset.line);
                }
                var tcount = 0;
                foreach (var line in g.Select(l => l.line))
                {
                    //ranks[line.id] = count;
                    ranks.Add(line.Id, count);
                    if (tcount==0)
                        tmpLinelist.Add(line);
                    tcount++;
                }
                if (StaticConfigParams.ComputeSanityChecks && tcount>1) 
                    throw new ArgumentException("We found more than two lines intersecting at the same point, this is not supported currently.");
                count++;
            }
            LineList = tmpLinelist.ToArray();
        }

        public void ResetVisitationData()
        {
            LeftCellsVisited = new bool[count];
            RightCellsVisited = new bool[count];
            SegmentToCellMap = new Cell[count];
        }

        public void PopulateSegmentData(Line myLine, SortedIntersectionData ds, bool reset = true)
        {
            Segments = new LineSegment[count - 1];
            _segmentsInv = new LineSegment[count - 1];
            var i = 0;
            foreach (var cellWall in LineList.Zip(LineList.Skip(1), (a, b) => new { First = a, Second = b }))
            {
                Segments[i] = new LineSegment(myLine, cellWall.First, cellWall.Second, i, ds);
                _segmentsInv[i] = new LineSegment(myLine, cellWall.Second, cellWall.First, i, ds);
                i++;
            }
            if (reset)
                ResetVisitationData();
        }


        /// <summary>
        /// Given a coordinate along the line, finds the flanking lines to it, and returns the relevant segment
        /// </summary>
        /// <returns></returns>
        public LineSegment GetSegmentFromCoordinate(Coordinate coord)
        {
            var closestLine = LineToCoordinate.OrderBy(lc => lc.Value.EuclideanDistance(coord)).First().Key;
            var segs = GetSegmentsFromLine(closestLine).ToList();
            return segs.FirstOrDefault(t => t.ContainsCoordinate(coord));
        }

        /// <summary>
        /// Given a line intersecting the source line, finds the flanking segments to it.
        /// </summary>
        /// <param name="source"></param>
        /// <returns></returns>
        public IEnumerable<LineSegment> GetSegmentsFromLine(Line source)
        {
            lock (locker)
            {
                var rank = ranks[source.Id];
                if ((rank + 1) <= (count - 1))
                    yield return Segments[rank];
                if ((rank - 1) >= 0)
                    yield return _segmentsInv[rank - 1];
            }
        }

        public LineSegment GetSegmentInverse(LineSegment seg)
        {
            if (Segments[seg.IdOnSource].FirstIntersection.Id == seg.FirstIntersection.Id)
                return _segmentsInv[seg.IdOnSource];
            else
                return Segments[seg.IdOnSource];
        }



        public IEnumerable<Line> GetLineNeighbors(Line source)
        {
            lock (locker)
            {
                var rank = ranks[source.Id];
                if ((rank + 1) <= (count - 1))
                    yield return LineList[rank + 1];
                if ((rank - 1) >= 0)
                    yield return LineList[rank - 1];
            }
        }

        

        public void CoverSegment(LineSegment lineSegment, Tesselation.SegmentCellCovered markedDirection, Cell cell)
        {

            switch (markedDirection)
            {
                case Tesselation.SegmentCellCovered.Left:
                    lock (locker)
                    {
                        LeftCellsVisited[lineSegment.IdOnSource] = true;
                        if (RightCellsVisited[lineSegment.IdOnSource] && cell != null && SegmentToCellMap[lineSegment.IdOnSource] != null)
                            SegmentToCellMap[lineSegment.IdOnSource].PairCellsByNeighbor(lineSegment, cell);
                        else
                            SegmentToCellMap[lineSegment.IdOnSource] = cell;
                    }
                    break;
                case Tesselation.SegmentCellCovered.Right:
                    lock (locker)
                    {
                        RightCellsVisited[lineSegment.IdOnSource] = true;
                        if (LeftCellsVisited[lineSegment.IdOnSource] && cell != null && SegmentToCellMap[lineSegment.IdOnSource] != null)
                            SegmentToCellMap[lineSegment.IdOnSource].PairCellsByNeighbor(lineSegment, cell);
                        else
                            SegmentToCellMap[lineSegment.IdOnSource] = cell;
                    }
                    break;
            }
        }

        public bool WasCovered(LineSegment lineSegment, Tesselation.SegmentCellCovered direction = Tesselation.SegmentCellCovered.Both)
        {
            switch (direction)
            {
                case Tesselation.SegmentCellCovered.Both:
                    return LeftCellsVisited[lineSegment.IdOnSource] && RightCellsVisited[lineSegment.IdOnSource];
                case Tesselation.SegmentCellCovered.Left:
                    return LeftCellsVisited[lineSegment.IdOnSource];
                case Tesselation.SegmentCellCovered.Right:
                    return RightCellsVisited[lineSegment.IdOnSource];
                case Tesselation.SegmentCellCovered.None:
                    return false;
                default:
                    throw new ArgumentOutOfRangeException(nameof(direction), direction, null);
            }
        }

        public bool ContainsLine(Line l)
        {
            return this.LineToCoordinate.ContainsKey(l);
        }

        public void AddLineToExistingData(Line cellline, SortedIntersectionData ds)
        {
            var icoord = cellline.Intersection(this.MyLine);
            lock (locker)
            {
                if (!CoordinateToLine.ContainsKey(icoord))
                    CoordinateToLine.Add(icoord, new HashSet<Line>());
                CoordinateToLine[icoord].Add(cellline);
                CoordinateToLine[icoord].Add(this.MyLine);
                LineToCoordinate.GetOrAdd(cellline, icoord);
                var lineLst = LineList.ToList();
                var loc = lineLst.BinarySearch(cellline, new LineIntersectionComparer(LineToCoordinate));
                if (loc < 0)
                {
                    lineLst.Insert(~loc, cellline);
                }
                else
                    throw new Exception("something is bad.");

                var intersectionlst =
                    lineLst.Select(l => new {line = l, intersection = LineToCoordinate[l]})
                        .GroupBy(l => l.intersection);

                count = 0;
                var tmpLinelist = new List<Line>();
                foreach (var g in intersectionlst)
                {
                    var tcount = 0;
                    foreach (var line in g.Select(l => l.line))
                    {
                        ranks[line.Id] = count;
                        if (tcount == 0)
                            tmpLinelist.Add(line);
                        tcount++;
                    }
                    if (StaticConfigParams.ComputeSanityChecks && tcount > 1)
                        throw new ArgumentException("We found more than two lines intersecting at the same point, this is not supported currently.");
                    count++;
                }
                LineList = tmpLinelist.ToArray();
                
                if (Segments.Length != (count - 1))
                {
                    var seglst = Segments.ToList();
                    var invseglst = _segmentsInv.ToList();
                    LeftCellsVisited = new bool[count];
                    
                    var _leftCellsVisited = LeftCellsVisited.ToList();
                    var _rightCellsVisited = RightCellsVisited.ToList();
                    var _segmentToCellMap = SegmentToCellMap.ToList(); 

                    var idx = tmpLinelist.IndexOf(cellline);
                    if (idx == 0)
                    {
                        seglst.Insert(0, new LineSegment(MyLine, cellline, tmpLinelist[1], 0, ds));
                        invseglst.Insert(0, new LineSegment(MyLine, tmpLinelist[1], cellline, 0, ds));
                        _leftCellsVisited.Insert(0, false);
                        _rightCellsVisited.Insert(0, false);
                        _segmentToCellMap.Insert(0, null);
                    }
                    else if (idx == tmpLinelist.Count - 1)
                    {
                        seglst.Insert(idx - 1, new LineSegment(MyLine, tmpLinelist[idx - 1], cellline, idx, ds));
                        invseglst.Insert(idx - 1, new LineSegment(MyLine, cellline, tmpLinelist[idx - 1] , idx, ds));
                        _leftCellsVisited.Insert(idx - 1, false);
                        _rightCellsVisited.Insert(idx - 1, false);
                        _segmentToCellMap.Insert(idx - 1, null);
                    }
                    else
                    {
                        //insert
                        seglst.RemoveAt(idx - 1);
                        invseglst.RemoveAt(idx - 1);
                        _leftCellsVisited.RemoveAt(idx - 1);
                        _rightCellsVisited.RemoveAt(idx - 1);
                        _segmentToCellMap.RemoveAt(idx - 1);
                        seglst.Insert(idx - 1, new LineSegment(MyLine, tmpLinelist[idx - 1], cellline, idx, ds));
                        invseglst.Insert(idx - 1, new LineSegment(MyLine, cellline, tmpLinelist[idx - 1], idx, ds));
                        _leftCellsVisited.Insert(idx - 1,false);
                        _rightCellsVisited.Insert(idx - 1, false);
                        _segmentToCellMap.Insert(idx - 1, null);
                        seglst.Insert(idx, new LineSegment(MyLine, cellline, tmpLinelist[idx + 1], idx + 1, ds));
                        invseglst.Insert(idx, new LineSegment(MyLine, tmpLinelist[idx + 1], cellline, idx + 1, ds));
                        _leftCellsVisited.Insert(idx, false);
                        _rightCellsVisited.Insert(idx, false);
                        _segmentToCellMap.Insert(idx, null);
                    }
                    for (var i = idx; i < seglst.Count; i++)
                    {
                        seglst[i].IdOnSource = i;
                        invseglst[i].IdOnSource = i;
                    }
                    /*
                    foreach (var cellWall in LineList.Zip(LineList.Skip(1), (a, b) => new { First = a, Second = b }))
                    {
                        if (cellWall.First == cellline || cellWall.Second == cellline)
                        {
                            if (i > 0 && i < Segments.Length)
                            {
                                seglst[i] = new LineSegment(MyLine, cellWall.First, cellWall.Second, i, ds);
                                invseglst[i] = new LineSegment(MyLine, cellWall.Second, cellWall.First, i, ds);
                            }
                            else
                            {
                                seglst.Insert(i, new LineSegment(MyLine, cellWall.First, cellWall.Second, i, ds));
                                invseglst.Insert(i, new LineSegment(MyLine, cellWall.Second, cellWall.First, i, ds));
                            }
                        }
                        else
                        {
                            seglst[i].IdOnSource = i;
                            invseglst[i].IdOnSource = i;
                        }
                        i++;
                    }
                    */
                    Segments = seglst.ToArray();
                    _segmentsInv = invseglst.ToArray();
                    LeftCellsVisited = _leftCellsVisited.ToArray();
                    RightCellsVisited = _rightCellsVisited.ToArray();
                    SegmentToCellMap = _segmentToCellMap.ToArray();
                }
            }
                    
        }

        public LineSegment GetSegment(Line Left, Line Right)
        {
            var rank = ranks[Left.Id];
            if ((rank + 1) <= (count - 1) && Segments[rank].SecondIntersection.Id == Right.Id)
                return Segments[rank];
            if ((rank - 1) >= 0 && _segmentsInv[rank - 1].FirstIntersection.Id == Left.Id)
                 return _segmentsInv[rank - 1];
            return null;
        }
    }

    public class LineIntersectionComparer : IComparer<Line>
    {
        private readonly ConcurrentDictionary<Line, Coordinate> _lineToCoordinate;

        public LineIntersectionComparer(ConcurrentDictionary<Line, Coordinate> lineToCoordinate)
        {
            _lineToCoordinate = lineToCoordinate;
        }

        public int Compare(Line x, Line y)
        {
            var clx = _lineToCoordinate[x];
            var cly = _lineToCoordinate[y];
            if (clx.X < cly.X)
                return -1;
            if (clx.X > cly.X)
            {
                return 1;
            }
            if (clx.Y < cly.Y)
                return -1;
            if (clx.Y > cly.Y)
            {
                return 1;
            }
            return 0;
        }
    }

}
