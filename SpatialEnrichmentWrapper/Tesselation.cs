using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data.SqlTypes;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Remoting.Messaging;
using System.Runtime.Remoting.Metadata.W3cXsd2001;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ResearchCommonLib.DataStructures;
using SpatialEnrichment;
using SpatialEnrichment.Helpers;
using SpatialEnrichmentWrapper;
using Accord.Statistics.Analysis;

namespace SpatialEnrichment
{
    
    public class Tesselation
    {
        #region Cell
        private int cellCount = 0, completeCellCount = 0;
        private double EstimatedCellCount;
        private readonly BlockingCollection<Cell> cellCollection;
        private readonly ConcurrentPriorityQueue<double,Cell> cellPQ = new ConcurrentPriorityQueue<double, Cell>();
        private readonly ConcurrentDictionary<Coordinate, byte> centroidVisitationCounter;
        //private ConcurrentDictionary<Tuple<int,int,int>, bool> segmentCoverDictionary = new ConcurrentDictionary<Tuple<int, int, int>, bool>();
        //Segment mapped to a single cells
        #endregion

        #region Line
        //public SortedIntersectionData LineIntersectionStruct;
        public static List<Line> Lines;
        private bool[] DepletedLines;
        //private ConcurrentDictionary<LineSegment, SegmentCellCovered> segmentCycles; 

        [Flags]
        public enum SegmentCellCovered : byte  { None = 0, Right = 1, Left = 2, Both = Right | Left};

        #endregion

        #region Linesegments
        //public static ConcurrentDictionary<Tuple<int, int, int>, SegmentCellCovered> BannedSegments = new ConcurrentDictionary<Tuple<int, int, int>, SegmentCellCovered>();
        #endregion

        #region Coordinates

        private List<string> Identities;
        private bool[] PointLabels;
        private List<ICoordinate> Points;
        private List<Coordinate> ConvexHull;
        #endregion


        private static Stopwatch sw = new Stopwatch();
        private Task convxTsk;

        public List<ICoordinate> ProjectedFrom = null;
        public PrincipalComponentAnalysis pca = null;
        private ConfigParams Config;

        public Tesselation(List<Coordinate> points, List<bool> labels, List<string> idendities, ConfigParams cnf)
        {
            Config = cnf;
            cellCollection = new BlockingCollection<Cell>();
            centroidVisitationCounter = new ConcurrentDictionary<Coordinate, byte>(new CoordinateComparer());
            //LineIntersectionStruct = null;
            Lines = null;
            Points = points.Cast<ICoordinate>().ToList();
            labels = labels ?? Enumerable.Repeat(true, points.Count).ToList();
            PointLabels = labels.ToArray();
            Identities = idendities.Any() ? idendities : null;
            convxTsk = ComputeConvexHull(points);

            Line.InitNumPoints(points.Count);
            Lines = new List<Line>();
            int ignoredLines = 0;
            for (var i = 0; i < points.Count; i++)
                for (var j = i + 1; j < points.Count; j++)
                    if (labels[i] != labels[j])
                    {
                        var line = Line.Bisector(points[i], points[j]);
                        var lineNormVec = new Coordinate(line.Slope, -1);
                        var d = -line.Intercept;
                        var pointSide = points.Select(p => (lineNormVec.DotProduct(p)-d)>0).ToList(); //On which side of plane is the point?
                        var isOneSidedProblem = labels.Zip(pointSide, (l, s) => new { Label= l, Side =s}).Where(p=>p.Label==true).Select(p=> p.Side).ToList();
                        if ((isOneSidedProblem.All(p => p) || isOneSidedProblem.All(p => !p)) && 
                            (Config.ActionList & Actions.Filter_DegenerateLines) != 0 && points.Count > 100)
                        {
                            //ignore lines where all points of 'true' label are located on one side
                            ignoredLines++;
                            Line.Count--;
                        }
                        else
                        {
                            line.SetPointIds(i, j);
                            Lines.Add(line);
                        }
                    }
            Console.WriteLine(@"Found {0} lines. {1} were degenerate sub problems and ignored.", Lines.Count, ignoredLines);
            EstimatedCellCount = ((long)Lines.Count * (Lines.Count - 1)) / 2.0 + Lines.Count + 1;
            DepletedLines = new bool[Lines.Count];
            Generics.SaveToCSV(Lines.Select(l => new Coordinate(l.Slope, l.Intercept)).ToList(), string.Format(@"lines_{0}.csv", StaticConfigParams.filenamesuffix));
        }

        public void LineSweep()
        {
            var linePairs = Lines.Zip(Lines.Skip(1), (a, b) => new { First = a, Second = b })
                .Select((t, idx) => new { Index = idx, t.First, t.Second })
                .ToDictionary(p => p.Index, p => new { p.First, p.Second });

            var AllCoords = new ConcurrentBag<Coordinate>();
            Parallel.ForEach(linePairs, pair =>
             {
                 var coord = pair.Value.First.Intersection(pair.Value.Second);
                 coord.CoordId = pair.Key;
                 AllCoords.Add(coord);
             });
            var cArray = AllCoords.OrderBy(c => c.CoordId).ToArray(); //maps coordid to coord
            var cycMap = new CoordNeighborArray[Lines.Count]; //per lines, which coordinates have appeared on it
            for (var i = 0; i < Lines.Count; i++) cycMap[i] = new CoordNeighborArray();
            var traversalOrder = AllCoords.OrderBy(c => c.X);
            int ptCount = 0;
            foreach(var c in traversalOrder)
            {
                var clines = linePairs[c.CoordId.Value];
                var start = clines.First.Id;
                var end = clines.Second.Id;
                cycMap[start].AddNeighbor(c.CoordId.Value);
                cycMap[end].AddNeighbor(c.CoordId.Value);
                var cycleRoot = -1; //holds the id of the line on which we see a cycle
                if (cycMap[start].Neighbors.Count() == 2)  //if we have seen two points on the line - cycle!
                    cycleRoot = start;
                if (cycMap[end].Neighbors.Count() == 2)
                    cycleRoot = end;
                if(cycleRoot>=0)
                {
                    var clst = new List<Coordinate>();
                    var seglst = new List<LineSegment>();
                    clst.AddRange(cycMap[cycleRoot].Neighbors.Select(cid => cArray[cid]));
                    seglst.Add(new LineSegment(clst.Skip(clst.Count - 2).First(), clst.Last()));
                    LineSegment lastSeg = null;
                    bool changed = true;
                    while (clst.Take(clst.Count-1).Any(tc=>tc.CoordId==clst.Last().CoordId) && changed)
                    {
                        changed = false;
                        var lastPair = clst.Skip(clst.Count - 2).ToArray();
                        lastSeg = seglst.Last();
                        //lastSeg.IsPositiveLexicographicProgress()
                        var linesOfLastCoord = linePairs[clst.Last().CoordId.Value];
                        var coordsOnLines = cycMap[linesOfLastCoord.First.Id].Neighbors.
                            Union(cycMap[linesOfLastCoord.Second.Id].Neighbors).ToList();
                        foreach (var tc in coordsOnLines)
                            if (!clst.Skip(clst.Count - 2).Any(v => v.CoordId.Equals(tc)))
                            {
                                var ls = new LineSegment(clst.Last(), cArray[tc]);
                                if(ls.IsPositiveLexicographicProgress() ^ lastSeg.IsPositiveAngle(ls))
                                {
                                    seglst.Add(ls);
                                    clst.Add(cArray[tc]);
                                    changed = true;
                                }
                            }
                    }
                    if(clst.First().CoordId == clst.Last().CoordId)
                        Console.WriteLine("Cycle found.");
                }
                if (ptCount % 100 == 0)
                    Console.WriteLine("\r\r\r\rCovered {0} points.",ptCount);
                ptCount++;
            }
        }

        public void GenerateFromCoordinates()
        {
            //Each line intersection defines two cells (left and right of line)
            //each consecutive intersection pair defines a wall of a cell
            //the coordinate overlap of walls identifies the cell border
            //identifying cycles in the graph induced by coordinates as nodes, and edges as consecutiveness yields cells.
            Console.WriteLine(@"Building intersection data.");
            var ds = BuildIntersectionsDatastruct(Lines);
            Console.WriteLine(@"Traversing graph.");
            
            var traverseGraphFindCycles = SearchCycles(ds);
            var computeMHGtsk = ComputeCellMHG();
            var prntTsk = Task.Run(() =>
            {
                var lastcount = 0; 
                var lastCompCount = 0;
                while (traverseGraphFindCycles.Status == TaskStatus.Running || computeMHGtsk.Status == TaskStatus.Running)
                {
                    if (cellCount - lastcount <= 100 && completeCellCount - lastCompCount <= 100) continue;
                    Console.Write("\r\r\r\rFound {0} cells. Covered {1}.", cellCount, completeCellCount);
                    lastcount = cellCount;
                    lastCompCount = completeCellCount;
                    Thread.Sleep(200);
                }
                Thread.Sleep(1000);
                Console.Write("\r\r\r\rFound {0} cells. Covered {1}.", cellCount, completeCellCount);
                Console.WriteLine();
            });
            
            Task.WaitAll(traverseGraphFindCycles, computeMHGtsk, prntTsk, convxTsk);
            Console.WriteLine(string.Join(";",
                centroidVisitationCounter.OrderByDescending(t => t.Value).Take(10).Select(t => t.Key + ":" + t.Value)));
        }

        private Task ComputeConvexHull(List<Coordinate> points)
        {
            return Task.Run(() =>
            {
                points.ToList().Sort((a, b) => Math.Abs(a.X - b.X) < StaticConfigParams.TOLERANCE ? a.Y.CompareTo(b.Y) : (a.X > b.X ? 1 : -1));
                List<Coordinate> hull = new List<Coordinate>();
                int L = 0, U = 0; // size of lower and upper hulls
                // Builds a hull such that the output polygon starts at the leftmost point.
                for (int i = points.Count - 1; i >= 0; i--)
                {
                    Coordinate p = points[i], p1;
                    // build lower hull (at end of output list)
                    while (L >= 2 && ((p1 = hull.Last()) - (hull[hull.Count - 2])).CrossProduct(p-p1) >= 0)
                    {
                        hull.RemoveAt(hull.Count - 1);
                        L--;
                    }
                    hull.Add(p);
                    L++;

                    // build upper hull (at beginning of output list)
                    while (U >= 2 && ((p1 = hull.First()) - (hull[1])).CrossProduct(p - p1) <= 0)
                    {
                        hull.RemoveAt(0);
                        U--;
                    }
                    if (U != 0) // when U=0, share the point added above
                        hull.Insert(0, p);
                    U++;
                }
                hull.RemoveAt(hull.Count - 1);
                ConvexHull = hull;
                Console.WriteLine(@"Done computing convex hull.");
            });
        }

        private SortedIntersectionData BuildIntersectionsDatastruct(List<Line> lines)
        {
            var LineIntersectionStruct = new SortedIntersectionData(lines.Count);
            int completedLines = 0, lastcount = 0;
            Parallel.For(0, lines.Count, i =>
            {
                var res = new SortedIntersectionList(i, lines);
                LineIntersectionStruct[i] = res;
                res.PopulateSegmentData(lines[i],LineIntersectionStruct);
                if (Interlocked.Increment(ref completedLines) - lastcount != 10) return;
                lastcount = completedLines;
                Console.Write("\r\r\rPrepared {0} line data structures.", completedLines);
            });
            Console.Write("\r\r\rPrepared {0} line data structures.", completedLines);
            Console.WriteLine();
            return LineIntersectionStruct;
        }

        private Task SearchCycles(SortedIntersectionData ds)
        {
            //segmentCycles = new ConcurrentDictionary<LineSegment, SegmentCellCovered>(new LineSegmentComparer());
            return Task.Run(() =>
            {
                Parallel.ForEach(ds,
                    new ParallelOptions()
                    {
                        MaxDegreeOfParallelism = (int) (StaticConfigParams.ExploreExploitRatio*Environment.ProcessorCount)
                    },
                    line =>
                    {
                        foreach (var segment in line.Segments.Where(segment => !IsSegmentIrrelevant(ds, segment,SegmentCellCovered.Both)))
                        {
                            foreach (var cell in BFS(segment, ds))
                                cellCollection.Add(cell);
                        }
                    });
                    
                cellCollection.CompleteAdding();
                Generics.SaveToCSV(centroidVisitationCounter.Keys.ToList(), string.Format(@"results_{0}.csv",StaticConfigParams.filenamesuffix));
            });
        }

        private bool IsSegmentIrrelevant(SortedIntersectionData ds, LineSegment segment, SegmentCellCovered markedDirection)
        {
            //Check if segment was assigned to two cells already
            var WasCovered = ds[segment.Source.Id].WasCovered(segment);
            //var wasBanned = BannedSegments.ContainsKey(segment.TupleId()) && (BannedSegments[segment.TupleId()] & markedDirection) != 0;
            //Check if segment is within convexhull
            //var IsntInHull = !(IsCoordInHull(segment.FirstIntersectionCoord) && IsCoordInHull(segment.SecondIntersectionCoord));
            return WasCovered; //|| wasBanned; //|| IsntInHull;
        }


        public bool IsCoordInHull(Coordinate test)
        {
            convxTsk.Wait();
            if (test == null) return false;
            int i, j;
            var result = false;
            for (i = 0, j = ConvexHull.Count - 1; i < ConvexHull.Count; j = i++)
            {
                if ((ConvexHull[i].Y > test.Y) != (ConvexHull[j].Y > test.Y) &&
                    (test.X <
                     (ConvexHull[j].X - ConvexHull[i].X)*(test.Y - ConvexHull[i].Y)/(ConvexHull[j].Y - ConvexHull[i].Y) +
                     ConvexHull[i].X))
                {
                    result = !result;
                }
            }
            return result;
        }

        /// <summary>
        /// Given a line segment, do a bfs search to find the chordless cycle containing its edges
        /// 
        /// </summary>
        /// <param name="rootSegment"></param>
        /// <param name="sortLl"></param>
        /// <param name="segment"></param>
        private IEnumerable<Cell> BFS(LineSegment rootSegment, SortedIntersectionData ds, bool reset = false, bool? Leftdirected = null)
        {
            if (reset)
            {
                centroidVisitationCounter.Clear();
                ds.ResetVisitationData();
            }

            var segQueue = new Queue<SegmentPath>();
            //Add root segment to open paths
            segQueue.Enqueue(new SegmentPath() {rootSegment});

            while(segQueue.Count > 0)
            {
                //Take a path from list of open paths
                var curr = segQueue.Dequeue();
                var markedDirection = curr.LeftTurnAngle ^ curr.PositiveXProgress
                        ? SegmentCellCovered.Right
                        : SegmentCellCovered.Left;
                if (curr.Any(t => IsSegmentIrrelevant(ds, t, markedDirection)))
                    continue;
                //find adjacent nodes before starting node
                var startNode = curr.First();
                // Take the end coordinate on the segment, look at its neighboring segments.
                var endNode = curr.Last();

                var linesAtCoord =
                    ds[endNode.Source.Id].CoordinateToLine[endNode.GetCoordinate(CoordTypes.Second).First()];

                var intersectorNeighbors2Nd =
                    linesAtCoord.SelectMany(
                            sl =>
                                ds[sl.Id].GetSegmentsFromLine(endNode.Source))
                        .Select(seg =>
                        {
                            var angle = endNode.GetAngle(seg);
                            return new {Segment = seg, Angle = angle};
                        })
                        .Where(node =>
                            startNode.Source != node.Segment.Source && !IsSegmentIrrelevant(ds, node.Segment, SegmentCellCovered.None) &&
                            (!Leftdirected.HasValue || ((node.Segment.IsPositiveLexicographicProgress() ^ node.Angle > 180) & Leftdirected.Value))).ToList();
                

                var maxAngle = intersectorNeighbors2Nd.Any() ? intersectorNeighbors2Nd.Max(t => t.Angle) : 360;
                var minAngle = intersectorNeighbors2Nd.Any() ? intersectorNeighbors2Nd.Min(t => t.Angle) : 0;
                //only allow the most extreme angles, otherwise we get chords in the cycle.
                if (curr.Count > 1)
                    intersectorNeighbors2Nd = curr.LeftTurnAngle
                        ? intersectorNeighbors2Nd.Where(seg => seg.Angle > 180 && Math.Abs(seg.Angle - maxAngle) < StaticConfigParams.TOLERANCE)
                            .ToList()
                        : intersectorNeighbors2Nd.Where(seg => seg.Angle <= 180 && Math.Abs(seg.Angle - minAngle) < StaticConfigParams.TOLERANCE)
                            .ToList();
                else 
                    intersectorNeighbors2Nd =
                        intersectorNeighbors2Nd.Where(
                            seg =>
                                Math.Abs(seg.Angle - maxAngle) < StaticConfigParams.TOLERANCE ||
                                Math.Abs(seg.Angle - minAngle) < StaticConfigParams.TOLERANCE).ToList();

                var neighborSegments = intersectorNeighbors2Nd.ToList();

                foreach (var endneighbor in neighborSegments) //Try to extend current path with new segment
                {
                    var nodelist = curr.ToList();
                    //there are no chords in the path (cycle does not contain cycles)
                    if (StaticConfigParams.ComputeSanityChecks)
                        if (curr.Take(curr.Count-1).Skip(1).Any(node => node.SharesIntersection(endneighbor.Segment))) //Found chord in path graph
                            continue;

                    nodelist.Add(endneighbor.Segment);
                    if (nodelist.Count == 2) //Path too short to be cycle
                        segQueue.Enqueue(new SegmentPath(nodelist, endneighbor.Angle > 180,
                            endneighbor.Segment.IsPositiveLexicographicProgress()));
                    else
                    {
                        //Sanity (remove when optimizing): verify angles are correct across path
                        if (StaticConfigParams.ComputeSanityChecks)
                        {
                            var angleList = nodelist.Zip(nodelist.Skip(1), (a, b) => a.GetAngle(b) > 180).ToList();
                            if (!(angleList.All(t => t) || !angleList.Any(t => t)))
                                throw new ApplicationException("angles changed.");
                        }

                        if (startNode.SharesIntersection(endneighbor.Segment)) //We have found a cycle!
                        {
                            var cell = new Cell(nodelist);
                            var cOm = cell.CenterOfMass;
                            
                            if (!centroidVisitationCounter.ContainsKey(cOm))
                            {
                                foreach (var lineSegment in nodelist)
                                {
                                    ds[lineSegment.Source.Id].CoverSegment(lineSegment, markedDirection, cell);
                                }
                                
                                if (!reset)
                                {
                                    cell.SetId(Interlocked.Increment(ref cellCount));
                                    if (StaticConfigParams.WriteToCSV)
                                        cell.SaveToCSV(string.Format(@"Cells\Cell_{0}_{1}.csv", cellCount, StaticConfigParams.filenamesuffix));
                                }
                                yield return cell;
                            }
                            centroidVisitationCounter.AddOrUpdate(cOm, t => 1, (a, b) => (byte) (b + 1));
                        }
                        else //Extend BFS search path
                        {
                            segQueue.Enqueue(new SegmentPath(nodelist, curr.LeftTurnAngle, endneighbor.Segment.IsPositiveLexicographicProgress()));
                        }
                    }

                }
            }
            
        }

        private Cell DirectedCellFromSegment(CoordMesh ds, LineSegment s, SegmentCellCovered direction, out bool banned)
        {
            banned = false;
            var presumedRightAngle = direction == SegmentCellCovered.Right;
            var nextDirection = direction;
            var segQueue = new List<LineSegment> {s};
            var segDirections = new List<SegmentCellCovered> { direction };
            var prevSeg = s;
            while (segQueue.Count<3 || !prevSeg.SharesIntersection(s))
            {
                var nextSeg = ds.GetSegmentNeighbors(prevSeg, direction).FirstOrDefault();
                //Data structure does not contain a neighboring segment in the required direction
                if (nextSeg == null) 
                {
                    foreach (var seg in segQueue)
                        ds.CoverCoordPair(seg.FirstIntersectionCoord, seg.SecondIntersectionCoord, direction); //Cover the segment's points from the segment's 'direction' side
                    return null; 
                }
                nextDirection = nextSeg.IsPositiveLexicographicProgress() ^ s.IsPositiveLexicographicProgress()
                    ? (direction == SegmentCellCovered.Right ? SegmentCellCovered.Left : SegmentCellCovered.Right)
                    : nextDirection;
                //We have already crossed this segment in this direction, abort.
                if (ds.WasCovered(nextSeg, direction)) 
                {
                    banned = true;
                    foreach (var seg in segQueue.Zip(segDirections,(a,b)=> new {Segment=a, Direction=b}))
                        ds.CoverCoordPair(seg.Segment.FirstIntersectionCoord, 
                            seg.Segment.SecondIntersectionCoord, direction); //Cover the segment's points from the segment's 'direction' side
                    return null;
                }
                segQueue.Add(nextSeg);
                segDirections.Add(nextDirection);
                prevSeg = nextSeg;
            }
            //if we have exited the loop we have found a cycle
            var cell = new Cell(segQueue);
            //Cover the segment's points from the segment's 'direction' side
            foreach (var seg in segQueue)
            {
                ds.CoverCoordPair(seg.FirstIntersectionCoord,
                           seg.SecondIntersectionCoord, direction); 
            }
            var cOm = cell.CenterOfMass;
            return cell;
        }

        private Task ComputeCellMHG()
        {
            return Task.Run(() =>
            {
                var tsklst = new List<Task>();
                var cellmHGs = new ConcurrentDictionary<Coordinate, Tuple<Cell, double, int, double>>();
                var topCellmHGs = new MinHeap<Tuple<Cell, double, int>>(null, 20, new mHGresultComparer());

                var printSignificantCells = new BlockingCollection<Tuple<Coordinate, double, int, double>>();
                var prnter = Task.Run(() =>
                {
                    int enrichedCounter = 0;
                    using (var fout = new StreamWriter(string.Format(@"Cells\EnrichedCells_{0}.csv",StaticConfigParams.filenamesuffix)))
                        foreach (var cell in printSignificantCells.GetConsumingEnumerable())
                        {
                            fout.WriteLine(cell.Item1 + "," + cell.Item2 + "," + cell.Item4);
                            //cell.Item1.SaveToCSV(string.Format(@"Cells\EnrichedCell_{0}_{1}_{2}.csv", enrichedCounter, cell.Item3, cell.Item2));
                            enrichedCounter++;
                        }
                });
                foreach (var cell in cellCollection.GetConsumingEnumerable())
                {
                    if (tsklst.Count > StaticConfigParams.CONST_CONCURRENCY)
                    {
                        var tid = Task.WaitAny(tsklst.ToArray());
                        tsklst.RemoveAt(tid);
                    }
                    var currCell = cell;
                    tsklst.Add(Task.Run(() =>
                    {
                        var neighbors = currCell.GetNeighbors();
                        var precalced = neighbors.Where(c => c.Value != null && c.Value.mHG != null).ToList();

                        //if (precalced.Any() && !precalced.Any(t => t.mHG.Item1 < 10*StaticConfigParams.CONST_SIGNIFICANCE_THRESHOLD)) return;
                        currCell.ComputeRanking(Points, PointLabels, Identities);
                        var res = currCell.Compute_mHG(StaticConfigParams.CorrectionType, Config);
                        var sarea = currCell.SurfaceAreaSimple();
                        var tres = new Tuple<Cell, double, int, double>(currCell, res.Item1, res.Item2, sarea);
                        if (cellmHGs.Count < 20 || res.Item2 < cellmHGs.Values.Max(t => t.Item2))
                            cellmHGs.AddOrUpdate(currCell.CenterOfMass, t => tres, (a, b) => tres);

                        topCellmHGs.Insert(new Tuple<Cell, double, int>(currCell, res.Item1, res.Item2));
                        Interlocked.Increment(ref completeCellCount);
                        //printSignificantCells.Add(new Tuple<Coordinate, double, int, double>(currCell.CenterOfMass, tres.Item2, tres.Item3, tres.Item4));
                        if (StaticConfigParams.WriteToCSV)
                            Task.Run(()=>cell.SaveToCSV(string.Format(@"Cells\CellHit{0}_{1}.csv", cell.MyId, StaticConfigParams.filenamesuffix)));
                        if (res.Item1 < Config.SIGNIFICANCE_THRESHOLD)
                        {
                            printSignificantCells.Add(new Tuple<Coordinate, double, int, double>(currCell.CenterOfMass, tres.Item2, tres.Item3, tres.Item4));
                        }
                    }));
                }
                Task.WaitAll(tsklst.ToArray());
                printSignificantCells.CompleteAdding();
                prnter.Wait();
            });
        }

        /// <summary>
        /// Utilizes the insight that cell all bounderies are consecutive items in the induced ordering on points.
        /// The closest cell wall is the bisector of the two points that are as close to equi-distant to the pivot as possible.
        /// 
        /// </summary>
        /// <param name="coord">pivot coordinate</param>
        /// <returns></returns>

        public IEnumerable<Cell> GradientSkippingSweep(int numStartCoords, int numThreads)
        {
            var bestCells = new ConcurrentPriorityQueue<double,Cell>();
            var coordLst = new List<Coordinate>();
            var tskLst = new List<Task>();
            //var sortLL = new SortedIntersectionData(Lines.Count);
            var cmesh = new CoordMesh(Lines);
            int TryCounter = 0;
            while (cellPQ.Count < numStartCoords && TryCounter < 10000) //cellPQ is a minHeap priotity queue (smaller key is better)
            {
                var coord = (Coordinate)Coordinate.MakeRandom();
                if (!IsCoordInHull(coord) && TryCounter < 1000)
                {
                    TryCounter++;
                    continue;
                }
                var strtCell = ComputeCellFromCoordinate(coord, cmesh);
                if (strtCell != null)
                {
                    cellPQ.Enqueue(Math.Log(strtCell.mHG.Item1), strtCell);
                    bestCells.Enqueue(Math.Log(strtCell.mHG.Item1), strtCell);
                    coordLst.Add(coord);
                }
            }
            
            var AssignMap = new ConcurrentDictionary<Coordinate, int>();
            foreach (var coord in coordLst)
                AssignMap.GetOrAdd(coord, 2); //avoid log ratio issues
            Generics.SaveToCSV(coordLst, $@"coordSample_{StaticConfigParams.filenamesuffix}.csv");
            var tskList = new List<Task>();
            KeyValuePair<double, Cell> currCell;
            bool init = true;
            while (tskList.Any(t => !t.IsCompleted) || init) //while any job is adding new cells
            {
                init = false;
                while (cellPQ.TryDequeue(out currCell)) //while we have cells in the queue
                {
                    if (tskList.Count == numThreads)
                    {
                        var tid = Task.WaitAny(tskList.ToArray());
                        tskList.RemoveAt(tid);
                    }
                    var cell = currCell.Value;
                    //assign this cell to the nearest start coord to keep track of its budget
                    tskList.Add(Task.Run(() => TraverseFromCell(cell, cmesh, AssignMap, bestCells)));
                }
                Task.WaitAll(tskLst.ToArray());
                tskList.Clear();
                Console.WriteLine("Finished traversal, resampling from leftovers.");
                var unseenCells = cmesh.GetUncoveredSegments().AsParallel()
                    .Select(seg => CoverCellFromSegment(cmesh, seg.Item1, seg.Item2))
                    .Where(t => t != null).Take(numStartCoords).ToList();
                foreach (var cell in unseenCells)
                    tskList.Add(Task.Run(() => TraverseFromCell(cell, cmesh, AssignMap, bestCells)));
            }
            return bestCells.Select(t => t.Value).OrderBy(t=>t.mHG.Item1);
        }

        private void TraverseFromCell(Cell cell, CoordMesh cmesh, ConcurrentDictionary<Coordinate, int> AssignMap,
            ConcurrentPriorityQueue<double, Cell> bestCells)
        {
            KeyValuePair<double, Cell> junk;
            foreach (var neigh in GetCellKthNeighbors(cell, cmesh).Where(n => n != null))
            {
                var history =
                    AssignMap.AddOrUpdate(
                        AssignMap.Keys.OrderBy(k => k.EuclideanDistance(neigh.CenterOfMass)).First(), 0,
                        (a, b) => b + 1);
                cellPQ.Enqueue(Math.Log(neigh.mHG.Item1) / Math.Log(history), neigh);
                bestCells.Enqueue(-Math.Log(neigh.mHG.Item1), neigh);
                while (bestCells.Count > Config.GetTopKResults) bestCells.TryDequeue(out junk);
            }
        }

        public SegmentCellCovered SegmentDirectionality(LineSegment s1, LineSegment s2)
        {
            return s1.IsPositiveLexicographicProgress() ^ s1.IsPositiveAngle(s2)
                ? SegmentCellCovered.Right
                : SegmentCellCovered.Left;
        }

        public Cell ComputeCellFromCoordinate(Coordinate coord, CoordMesh sortLL)
        {
            bool banned = false;
            //Order points by distance from pivot
            var sortedDistances = Points.AsParallel()
                .Select((p,idx) => new {Point = p, Label=PointLabels[idx], PointId=idx, Distance = p.EuclideanDistance(coord)})
                .OrderBy(t=>t.Distance).ToArray();

            var relevantLines = new List<Line>();
            for (var i = 0; i < sortedDistances.Length-1;)
            {
                var consecGroup1 = sortedDistances.Skip(i).TakeWhile(p => p.Label == sortedDistances[i].Label).ToList(); //maps to identically significant neighbors
                var consecGroup2 = sortedDistances.Skip(i+consecGroup1.Count).TakeWhile(p => p.Label == !sortedDistances[i].Label).ToList();
                i = i + consecGroup1.Count;
                relevantLines.AddRange(from pt1 in consecGroup1
                    from pt2 in consecGroup2
                    select Lines[Line.LineIdFromCoordIds(pt1.PointId, pt2.PointId)]);
            }
            var OrderedLines =
                relevantLines.AsParallel()
                .Select(l => new {Line = l, Distance = l.DistanceToPoint(coord)})
                .OrderBy(l => l.Distance)
                .ToList();
            
            //find 'handeness' of first cell wall to pivot (should we turn left or right to contain?)
            var closestLine = OrderedLines.First().Line;
            var startCoord = closestLine.Perpendicular(coord).Intersection(closestLine);

            //while adding lines, if no cell yet, or if the chordless cell that contains the pivot coord changes its boundry, continue adding lines.
            Cell prevCell = null;
            var linecount = 0;
            var startSeg = sortLL.GetSegmentContainingCoordinateOnLine(closestLine.Id, startCoord);
            if (startSeg != null)
            {
                var segAngle = LineSegment.GetAngle(coord, startCoord, startCoord, startSeg.SecondIntersectionCoord);
                var direction = startSeg.IsPositiveLexicographicProgress() ^ (segAngle > 180) ? SegmentCellCovered.Right : SegmentCellCovered.Left;
                prevCell = CoverCellFromSegment(sortLL, startSeg, direction);
            }
            
            //GetCellKthNeighbors(prevCell, sortLL);
            return prevCell;
        }

        private Cell CoverCellFromSegment(CoordMesh sortLL, LineSegment startSeg, SegmentCellCovered direction)
        {
            bool banned;
            var cell = DirectedCellFromSegment(sortLL, startSeg, direction, out banned);
            if (banned || cell == null) return null;
            if (ProjectedFrom==null)
                cell.ComputeRanking(Points, PointLabels, Identities);
            else
                cell.ComputeRanking(ProjectedFrom, PointLabels, Identities, pca);
            cell.Compute_mHG(StaticConfigParams.CorrectionType, Config);
            cell.SetId(Interlocked.Increment(ref cellCount));
            if (cell.MyId % 100 == 0)
            {
                var numcovered = (int) (sortLL.segmentCount / 8);
                var percentCovered = (double) numcovered / sortLL.numCoords; //numcell / Config.Cellcount
                Console.Write("\r\r\r\r\r\r\r\rCell #{0} ({1:P1}) @{2} with {3:F}cps {4:E2}mHG est {5:g} remaining.", numcovered,//cellCount,
                    percentCovered, cell.CenterOfMass.ToString("0.000"),
                    numcovered / sw.Elapsed.TotalSeconds, mHGJumper.optHGT, 
                    new TimeSpan(0, 0, (int)((EstimatedCellCount - numcovered) / (numcovered / sw.Elapsed.TotalSeconds))));
                if (!sw.IsRunning)
                    sw.Start();
            }
            if (StaticConfigParams.WriteToCSV)
                Task.Run(() => cell.SaveToCSV($@"Cells\CellHit{cell.MyId}_{StaticConfigParams.filenamesuffix}.csv"));
            return cell;
        }

        private IEnumerable<Cell> GetCellKthNeighbors(Cell prevCell, CoordMesh sortLL)
        {
            //var requiredSkips = prevCell.mHG.Item3;
            foreach (var seg in prevCell.GetCellWalls())
            {
                if (DepletedLines[seg.Source.Id])
                    continue;
                //Check which side of the segment is contained in the cell
                var isLeftAngle = LineSegment.GetAngle(seg.FirstIntersectionCoord, seg.SecondIntersectionCoord,
                                    seg.FirstIntersectionCoord, prevCell.CenterOfMass) > 180;
                
                var sortedLines = sortLL.GetAllLinesAroundSegment(seg);
                var orderedLeftLines = sortedLines.Item1;
                var orderedRightLines = sortedLines.Item2;
                
                var leftSegment = NearestAllowedSegment(prevCell, sortLL, orderedLeftLines, seg, 
                    isLeftAngle ? SegmentCellCovered.Right : SegmentCellCovered.Left);
                var rightSegment = NearestAllowedSegment(prevCell, sortLL, orderedRightLines, seg, 
                    isLeftAngle ? SegmentCellCovered.Left : SegmentCellCovered.Right);

                //for allowed segments lets find their corresponding cells and continue with the recursion.
                var segList = new List<Tuple<LineSegment, SegmentCellCovered>>()
                {
                    new Tuple<LineSegment, SegmentCellCovered>(leftSegment, isLeftAngle ? SegmentCellCovered.Right : SegmentCellCovered.Left),
                    new Tuple<LineSegment, SegmentCellCovered>(rightSegment, isLeftAngle ? SegmentCellCovered.Left : SegmentCellCovered.Right)
                };
                foreach (var legalSeg in segList.Where(tseg => tseg.Item1!=null))
                    {
                        yield return CoverCellFromSegment(sortLL, legalSeg.Item1, legalSeg.Item2);
                    }
                if (leftSegment == null && rightSegment == null)
                    DepletedLines[seg.Source.Id] = true;

            }
        }

        private LineSegment NearestAllowedSegment(Cell prevCell, CoordMesh sortLL, IEnumerable<Line> sortedLines, LineSegment seg, SegmentCellCovered coverageDirection)
        {
            var SLe = sortedLines.GetEnumerator();
            //if (!sortedLines.Any()) return null;
            //We skip from the current segment and find the nearest allowed segment on both sides.

            int skipped = 0; //counts actual number of skips from prevCells
            LineSegment openSegment = null; //return value
            
            var boolVec = prevCell.InducedLabledVector.ToArray(); //state of point labels at current segment's cell
            var coordVec = new int[prevCell.InducedLabledVector.Length]; //B per rank at current segment's cell
            coordVec[0] = prevCell.InducedLabledVector[0] ? 1 : 0;
            for (var i = 1; i < prevCell.InducedLabledVector.Length; i++)
            {
                var val = (prevCell.InducedLabledVector[i] ? 1 : 0);
                coordVec[i] = coordVec[i - 1] + val;
            }
            var skipsArray = prevCell.mHG.Item3.ToArray(); //min number of skips per threshold to beat mHG opt
            Line lastLine = null;
            var lid = 0;
            while(openSegment == null && SLe.MoveNext())
            {
                var line = SLe.Current;
                //lid++;
                var ptArnk = prevCell.PointRanks[line.PointAId];
                var ptBrnk = prevCell.PointRanks[line.PointBId];
                if (ptArnk > ptBrnk) Generics.Swap(ref ptArnk, ref ptBrnk);
                
                //Adding a line to segment.
                Generics.Swap(ref boolVec[ptArnk], ref boolVec[ptBrnk]);
                for (var i = ptArnk; i < ptBrnk + 1; i++)
                    coordVec[i] = (i > 0 ? coordVec[i - 1] : 0) + (boolVec[i] ? 1 : 0);

                skipsArray[coordVec[ptArnk]] += boolVec[ptArnk] && !boolVec[ptBrnk] ? 1 : -1;
                    //raised a 0 and lowered a 1 in the vector
                skipsArray[coordVec[ptBrnk]] += boolVec[ptArnk] && !boolVec[ptBrnk] ? -1 : 1;
                    //raised a 1 and lowered a 0 in the vector

                var remainingSkips = skipsArray[coordVec[ptArnk]] + Config.SKIP_SLACK;
                if (lastLine != null)
                {
                    openSegment = sortLL.GetSegment(seg.Source.Id, lastLine, line);
                    skipped++;
                    var coverageExtension = remainingSkips > 1
                        ? SegmentCellCovered.Both
                        : coverageDirection;
                    var covered = sortLL.WasCovered(openSegment, coverageExtension);
                    if (remainingSkips > 0 && !covered)
                    {
                        sortLL.CoverSegment(openSegment, coverageExtension);
                        //if(Config.CellCountStrategy == )
                        Interlocked.Increment(ref cellCount);
                        if (remainingSkips > 1)
                            Interlocked.Increment(ref cellCount);
                        openSegment = null;
                    }
                    if (covered)
                        openSegment = null;
                }
                lastLine = line;
            }
            return openSegment;
        }

        private void GetCellNeighbors(Cell prevCell, CoordMesh sortLL)
        {
            foreach (var seg in prevCell.GetCellWalls())
            {
                var segId = seg.TupleId();
                //if (segmentCoverDictionary.GetOrAdd(segId, t => false)) continue;
                //segmentCoverDictionary.AddOrUpdate(segId, t => true, (a, b) => true);
                var mid = seg.MidPoint();
                var midPlusEps = new Coordinate(seg.Source.EvaluateAtY(mid.Y) + StaticConfigParams.TOLERANCE, mid.Y + StaticConfigParams.TOLERANCE);
                var midMinusEps = new Coordinate(seg.Source.EvaluateAtY(mid.Y) - StaticConfigParams.TOLERANCE, mid.Y - StaticConfigParams.TOLERANCE);
                // find a point that is surely inside the neighboring cell and call recursively to identify cell usint it as pivot.
                Coordinate containingCoordinate;
                if (prevCell.ContainsCoord(midPlusEps) && !prevCell.ContainsCoord(midMinusEps))
                    containingCoordinate = midMinusEps;
                else
                {
                    if (!prevCell.ContainsCoord(midPlusEps) && prevCell.ContainsCoord(midMinusEps))
                        containingCoordinate = midPlusEps;
                    else
                        throw new Exception("Bad coordinate sample. Try reducing Program.Tolerence.");
                }
                var neighbor = ComputeCellFromCoordinate(containingCoordinate, sortLL);
                if (neighbor != null &&
                    ((Config.ActionList & Actions.Search_GradientDescent) == 0 ||
                     PointLabels[ClosestPointId(seg.Source, containingCoordinate)]))
                {
                    prevCell.PairCellsByNeighbor(seg, neighbor);
                    Task.Run(() =>
                    {
                        //neighbor.ComputeRanking(Points, PointLabels, Identities); //moved this inside the computecellfromcoordinate
                        //neighbor.Compute_mHG(Program.CorrectionType);
                        if (StaticConfigParams.WriteToCSV)
                        {
                            neighbor.SaveToCSV($@"Cells\CellHit{Interlocked.Increment(ref cellCount)}.csv");
                        }
                    });
                }
            }
        }


        private int ClosestPointId(Line line, Coordinate coord)
        {
            var p1dist = Points[line.PointAId].EuclideanDistance(coord);
            var p2dist = Points[line.PointBId].EuclideanDistance(coord);
            return p1dist < p2dist ? line.PointAId : line.PointBId;
        }

        public static void Reset()
        {
            //LineSegmentComparer.Reset();
            Line.Reset();
        }

        private class mHGresultComparer : IComparer<Tuple<Cell, double, int>>
        {
            public int Compare(Tuple<Cell, double, int> x, Tuple<Cell, double, int> y)
            {
                if (x.Item2 > y.Item2)
                    return 1;
                if (x.Item2 < y.Item2)
                    return -1;
                else
                    return 0;
            }
        }

    }

    internal class LineCoordDistance
    {
        public Line Line { get; private set; }
        public Coordinate Coordinate { get; private set; }
        public double LeftDistance, RightDistance;
        public LineCoordDistance(Line line, Coordinate coord, double leftDistance, double rightDistance)
        {
            Line = line;
            Coordinate = coord;
            LeftDistance = leftDistance;
            RightDistance = rightDistance;
        }
    }
}
