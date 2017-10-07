using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading.Tasks;
using SpatialEnrichment.Helpers;
using SpatialEnrichmentWrapper;
using Accord.Statistics.Analysis;
using System.Threading;

namespace SpatialEnrichment
{
    public class Coordinate : IEquatable<Coordinate>, ICoordinate
    {
        public readonly double X, Y;
        public int? CoordId;
        public Coordinate(double x, double y)
        {
            if (double.IsNaN(x) || double.IsNaN(y) || double.IsInfinity(x) || double.IsInfinity(y))
                throw new ApplicationException("Bad coordinate values");
            X = x>0 ? Math.Min(x, int.MaxValue / 1000.0) : Math.Max(x, int.MinValue / 1000.0);
            Y = y>0 ? Math.Min(y, int.MaxValue / 1000.0) : Math.Max(y, int.MinValue / 1000.0);
        }

        public double GetDimension(int dim)
        {
            switch (dim)
            {
                case 0:
                    return X;
                case 1:
                    return Y;
                default:
                    throw new NotImplementedException("Two dimensional data does not implement get dim >1!");
            }
        }

        public bool Equals(Coordinate other)
        {
            return (Math.Abs(this.X - other.X) < StaticConfigParams.TOLERANCE) && (Math.Abs(this.Y - other.Y) < StaticConfigParams.TOLERANCE);
        }

        public override int GetHashCode()
        {
            return Convert.ToInt32(31 * X + 17 * Y);
        }

        public override string ToString()
        {
            return ToString(@"0.00000000");
        }

        public string ToString(string fmt)
        {
            return X.ToString(fmt) + "," + Y.ToString(fmt);
        }

        public static Coordinate operator +(Coordinate curr, Coordinate other)
        {
            return new Coordinate(curr.X + other.X, curr.Y + other.Y);
        }

        public static Coordinate operator -(Coordinate curr, Coordinate other)
        {
            return new Coordinate(curr.X - other.X, curr.Y - other.Y);
        }

        public static bool IsPositiveLexicographicProgress(Coordinate from, Coordinate to)
        {
            if (from.X < to.X)
                return true;
            if (from.X > to.X)
                return false;
            if (from.Y < to.Y)
                return true;
            return false;
        }

        public double[] ToArray()
        {
            return new[] {X, Y};
        }

        public double Angle(Coordinate other)
        {
            return Math.Atan2(other.Y, other.X) - Math.Atan2(this.Y, this.X);
            //return Math.Acos(DotProduct(other)/(Math.Sqrt(this.DotProduct(this))*Math.Sqrt(other.DotProduct(other))));
        }

        public double DotProduct(Coordinate other)
        {
            return (this.X*other.X + this.Y*other.Y);
        }

        public double CrossProduct(Coordinate other)
        {
            return (this.X*other.Y - this.Y*other.X);
        }

        public double EuclideanDistance(ICoordinate other)
        {
            return Math.Sqrt(Math.Pow(this.X - ((Coordinate)other).X, 2) + 
                Math.Pow(this.Y - ((Coordinate)other).Y, 2));
        }

        public static ICoordinate MakeRandom()
        {
            return new Coordinate(StaticConfigParams.rnd.NextDouble(),StaticConfigParams.rnd.NextDouble());
        }

    }

    public class CoordinateComparer : IEqualityComparer<Coordinate>, IComparer<Coordinate>
    {
        public bool Equals(Coordinate x, Coordinate y)
        {
            return x.Equals(y);
        }

        public int GetHashCode(Coordinate obj)
        {
            return obj.GetHashCode();
        }

        public int Compare(Coordinate c1, Coordinate c2)
        {
            if (c1.X > c2.X)
                return 1;
            if (c1.X < c2.X)
                return -1;
            if (c1.Y > c2.Y)
                return 1;
            if (c1.Y < c2.Y)
                return -1;
            return 0;
        }
    }

    public class Line :IDisposable
    {
        public int PointAId = -1, PointBId = -1;
        public readonly int Id;
        public static int Count;
        //public static Dictionary<Tuple<int, int>, int> LineIdsMap = new Dictionary<Tuple<int, int>, int>();
        private static int[][] LineIdsMap;

        public double Slope, Intercept;
        public Line(double slope, double intercept, bool isCounted=true)
        {
            if (double.IsNaN(Slope) || double.IsNaN(Intercept) || double.IsInfinity(Slope) || double.IsInfinity(Intercept))
                throw new ApplicationException("Bad line values");
            if (Math.Abs(slope) < double.Epsilon)
            {
                Console.WriteLine(@"Line with slope 0 detected, adding epsilon.");
                slope += (StaticConfigParams.rnd.NextDouble() - 0.5) * StaticConfigParams.TOLERANCE;
            }
            Slope = slope;
            Intercept = intercept;
            if (isCounted)
                Id = Count++;
        }

        public void SetPointIds(int i, int j)
        {
            PointAId = i;
            PointBId = j;
            LineIdsMap[PointAId][PointBId] = this.Id;
        }

        public static int LineIdFromCoordIds(int line1, int line2)
        {
            var linea = Math.Min(line1, line2);
            var lineb = Math.Max(line1, line2);
            return LineIdsMap[linea][lineb];
        }

        public Coordinate Intersection(Line that)
        {
            var xcoord = (that.Intercept - this.Intercept) / (this.Slope - that.Slope);
            var ycoord = (this.Intercept * that.Slope - that.Intercept * this.Slope) / (that.Slope - this.Slope);
            return new Coordinate(xcoord, ycoord);
        }

        public static Line Bisector(Coordinate a, Coordinate b)
        {
            var midPoints = new Coordinate((a.X + b.X) / 2, (a.Y + b.Y) / 2);
            var slope = (a.X - b.X) / (b.Y - a.Y);
            var intercept = midPoints.Y - slope * midPoints.X;
            return new Line(slope, intercept);
        }

        public Line Perpendicular(Coordinate coord)
        {
            var recipSlope = -1.0/this.Slope;
            var perpIntercept = coord.Y - recipSlope*coord.X;
            return new Line(recipSlope, perpIntercept);
        }

        public bool Equals(Line other)
        {
            return Math.Abs(this.Slope - other.Slope) < StaticConfigParams.TOLERANCE &&
                   Math.Abs(this.Intercept - other.Intercept) < StaticConfigParams.TOLERANCE;
        }

        public override int GetHashCode()
        {
            return Id.GetHashCode();
        }

        public override string ToString()
        {
            return String.Format(@"{0}", Id);
        }
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
            }
        }

        public static void Reset()
        {
            Count = 0;
            //LineIdsMap.Clear();
        }

        public double DistanceToPoint(Coordinate coord)
        {
            //assuming line y=mx+k : normal to line crossing through P is y=(x_0-x)/m+y_0
            if (Slope == 0)
                return Math.Abs(coord.Y - this.Intercept);
            var normal = new Line(-1.0/Slope, coord.X/Slope + coord.Y,false);
            var intersectionPt = Intersection(normal);
            return intersectionPt.EuclideanDistance(coord);
        }

        /// <summary>
        /// Given a y value, returns the x value on the line
        /// </summary>
        /// <param name="that"></param>
        /// <returns></returns>
        public double EvaluateAtY(double y)
        {
            return (y - this.Intercept)/this.Slope;
        }

        public double EvaluateAtYSafe(Coordinate c)
        {
            if (Math.Abs(c.Y - this.Intercept) < StaticConfigParams.TOLERANCE && Math.Abs(this.Slope) < StaticConfigParams.TOLERANCE)
                return c.X;
            return (c.Y - this.Intercept) / this.Slope;
        }

        public bool Contains(Coordinate coord)
        {
            return (Math.Abs(coord.Y - (Slope*coord.X - Intercept)) < StaticConfigParams.TOLERANCE);
        }

        public static void InitNumPoints(int pointsCount)
        {
            LineIdsMap = new int[pointsCount][];
            for (var i=0; i<pointsCount; i++)
                LineIdsMap[i] = new int[pointsCount];
        }
    }


    public enum CoordTypes
            {
                Both,
                First,
                Second
            };

    public class LineSegment : IDisposable
    {
        public Line Source, FirstIntersection, SecondIntersection;
        public int IdOnSource { get; set; }
        public Line CanonicalFirst { get { return FirstIntersection.Id < SecondIntersection.Id ? FirstIntersection : SecondIntersection; } }
        public Line CanonicalSecond { get { return FirstIntersection.Id < SecondIntersection.Id ? SecondIntersection : FirstIntersection; } }
        public Coordinate FirstIntersectionCoord;
        public Coordinate SecondIntersectionCoord;
        private Tuple<int, int, int> _tupleId;

    
        public LineSegment(Line source, Line itx1, Line itx2, Coordinate first, Coordinate second)
        {
            Source = source;
            FirstIntersection = itx1;
            SecondIntersection = itx2;

            FirstIntersectionCoord = first;
            SecondIntersectionCoord = second;
        }

        //Arbitrary linesegment (not part of the datastructures)
        public LineSegment(Coordinate first, Coordinate second)
        {
            FirstIntersectionCoord = first;
            SecondIntersectionCoord = second;
        }

        public LineSegment(Line source, Line itx1, Line itx2, int idOnSource, SortedIntersectionData ds)
        {
            Source = source;
            FirstIntersection = itx1;
            SecondIntersection = itx2;
            IdOnSource = idOnSource;
            FirstIntersectionCoord = ds[source.Id].LineToCoordinate.GetOrAdd(itx1, source.Intersection(itx1));
            SecondIntersectionCoord = ds[source.Id].LineToCoordinate.GetOrAdd(itx2, source.Intersection(itx2));
        }

        public IEnumerable<Line> Intersections()
        {
            yield return FirstIntersection;
            yield return SecondIntersection;
        }

        public IEnumerable<Line> GetLines()
        {
            yield return this.Source;
            yield return this.CanonicalFirst;
            yield return this.CanonicalSecond;
        }

        public IEnumerable<Coordinate> GetCoordinate(CoordTypes type = CoordTypes.Both)
        {
            switch (type)
            {
                case CoordTypes.Both:
                    yield return FirstIntersectionCoord;
                    yield return SecondIntersectionCoord;
                    break;
                case CoordTypes.First:
                    yield return FirstIntersectionCoord;
                    break;
                case CoordTypes.Second:
                    yield return SecondIntersectionCoord;
                    break;
                default:
                    yield break;
            }
        }
        public bool SharesIntersection(LineSegment other)
        {
            var firstcoords = this.GetCoordinate().ToList();
            var secondcoords = other.GetCoordinate().ToList();
            if(this.Source!=null && other.Source != null)
                return (this.Source != other.Source) && firstcoords.Any(c => secondcoords.Contains(c));
            return firstcoords.Select(c => c.CoordId.Value).Intersect(secondcoords.Select(c => c.CoordId.Value)).Any();
        }

        public bool Equals(LineSegment other)
        {
            return (this.Source.Id == other.Source.Id) &&
                   ((this.FirstIntersection.Id == other.FirstIntersection.Id &&
                    this.SecondIntersection.Id == other.SecondIntersection.Id) ||
                   (this.FirstIntersection.Id == other.SecondIntersection.Id &&
                    this.SecondIntersection.Id == other.FirstIntersection.Id));
        }

        public override int GetHashCode()
        {
            return FirstIntersectionCoord.GetHashCode() ^ SecondIntersectionCoord.GetHashCode();
            //(Source.GetHashCode() << 7) ^ FirstIntersection.GetHashCode() ^ SecondIntersection.GetHashCode();
        }

        public void Dispose()
        {
            Dispose(true);  
            GC.SuppressFinalize(this);  
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                Source.Dispose();  
                FirstIntersection.Dispose();
                SecondIntersection.Dispose();
            }
        }  

        public override string ToString()
        {
            return string.Format(@"({0}){1}-{2}", Source, FirstIntersection, SecondIntersection);
        }

        public double GetAngle(LineSegment other)
        {
            var myCoords = GetCoordinate().ToArray();
            var otherCoords = other.GetCoordinate().ToArray();
            //if (StaticConfigParams.ComputeSanityChecks)
            if (myCoords[1].Equals(otherCoords[0]))
                return GetAngle(myCoords[0], myCoords[1], otherCoords[0], otherCoords[1]);
            if (myCoords[1].Equals(otherCoords[1]))
                return GetAngle(myCoords[0], myCoords[1], otherCoords[1], otherCoords[0]);
            if (myCoords[0].Equals(otherCoords[0]))
                return GetAngle(myCoords[1], myCoords[0], otherCoords[0], otherCoords[1]);
            if (myCoords[0].Equals(otherCoords[1]))
                return GetAngle(myCoords[1], myCoords[0], otherCoords[1], otherCoords[0]);
            throw new ApplicationException(@"Segments do not overlap in coordinate");
        }

        public static double GetAngle(Coordinate Seg1Coord1, Coordinate Seg1Coord2, Coordinate Seg2Coord1, Coordinate Seg2Coord2)
        {
            var vectorizeMe = new Coordinate(Seg1Coord2.X - Seg1Coord1.X, Seg1Coord2.Y - Seg1Coord1.Y);
            var myXAngle = Math.Atan2(vectorizeMe.Y, vectorizeMe.X) * (180.0 / Math.PI);
            var vectorizeOther = new Coordinate(Seg2Coord2.X - Seg2Coord1.X, Seg2Coord2.Y - Seg2Coord1.Y);
            var othersXAngle = Math.Atan2(vectorizeOther.Y, vectorizeOther.X) * (180.0 / Math.PI);
            var angle = (180.0 - myXAngle + othersXAngle) % 360;
            angle = angle < 0 ? 360 + angle : angle;
            return angle;
        }

        public bool IsPositiveAngle(LineSegment other)
        {
             return GetAngle(other) > 180;
        }

        public bool IsPositiveLexicographicProgress()
        {
            return (FirstIntersectionCoord.X < SecondIntersectionCoord.X) ||
                   (Math.Abs(FirstIntersectionCoord.X - SecondIntersectionCoord.X) < StaticConfigParams.TOLERANCE &&
                    FirstIntersectionCoord.Y > SecondIntersectionCoord.Y);
        }

        public bool CrossesY(double yval)
        {
            return (this.FirstIntersectionCoord.Y <= yval && yval <= this.SecondIntersectionCoord.Y) ||
                   (this.SecondIntersectionCoord.Y <= yval && yval <= this.FirstIntersectionCoord.Y);
        }

        public bool ContainsCoordinate(Coordinate coord)
        {
            var c1 = FirstIntersectionCoord;
            var c2 = SecondIntersectionCoord;
            //Establish coordinate order on X axis
            if (c1.X < c2.X)
            {
                c1 = SecondIntersectionCoord;
                c2 = FirstIntersectionCoord;
            }
            if (c1.X >= coord.X && coord.X >= c2.X) //Check if coordinate is between edges in X axis
            {
                //Establish coordinate order on Y axis
                if (c1.Y < c2.Y)
                {
                    var tmp = c1;
                    c1 = c2;
                    c2 = tmp;
                }
                if (c1.Y >= coord.Y && coord.Y >= c2.Y && Math.Abs(Source.EvaluateAtY(coord.Y)-coord.X)<StaticConfigParams.TOLERANCE)
                    return true; //Check if coordinate is between edges in Y axis
            }
            return false;
        }

        public void SaveAsCsv(string filename)
        {
            using (var file = new StreamWriter(filename))
            {
                file.WriteLine(this.FirstIntersectionCoord);
                file.WriteLine(this.SecondIntersectionCoord);
            }
        }

        public Coordinate MidPoint()
        {
            return new Coordinate((this.FirstIntersectionCoord.X + this.SecondIntersectionCoord.X)/2.0,
                (this.FirstIntersectionCoord.Y + this.SecondIntersectionCoord.Y)/2.0);
        }

        public Tuple<int,int,int> TupleId()
        {
            return _tupleId ?? (_tupleId = new Tuple<int, int, int>(Source.Id, CanonicalFirst.Id, CanonicalSecond.Id));
        }
    }

    public class SegmentPath : List<LineSegment>
    {
        public bool LeftTurnAngle { get; set; }
        public bool PositiveXProgress { get; set; }
        public SegmentPath() { }
        public SegmentPath(List<LineSegment> segments, bool angle, bool posXprogress)
        {
            this.AddRange(segments);
            LeftTurnAngle = angle;
            PositiveXProgress = posXprogress;
        }
    }


    public class Cell : IDisposable
    {
        private Dictionary<LineSegment,Cell> segments; //Maps a segment to its cell neighbor
        private List<LineSegment> segmentOrder;
        private Coordinate _cOm = null;
        private object locker = new object();

        private Tuple<double, int, int[]> _mHG;
        public Tuple<double, int, int[]> mHG
        {
            get
            {
                lock (locker)
                {
                    return _mHG;
                }
            }
        }

        public bool[] InducedLabledVector { get; private set; }

        public IEnumerable<Coordinate> Coordinates { get { return this.segments.Keys.Select(s => s.FirstIntersectionCoord); } }
        public IEnumerable<LineSegment> Segments { get { return this.segmentOrder; } }

        public int[] PointRanks { get; private set; }

        public Coordinate CenterOfMass
        {
            get
            {
                if (_cOm != null) return _cOm;
                var segCoords = segments.Keys.SelectMany(t => t.GetCoordinate()).ToList();
                var newX = segCoords.Select(c => c.X).Average();
                var newY = segCoords.Select(c => c.Y).Average();
                _cOm = new Coordinate(newX, newY);
                return _cOm;
            }
            private set { }
        }

        public int MyId { get; set; }

        public Cell(List<LineSegment> lineSegments)
        {
            segments = lineSegments.ToDictionary(t => t, t=>(Cell)null);
            segmentOrder = lineSegments;
        }

        public void PairCellsByNeighbor(LineSegment seg, Cell otherCell)
        {
            lock (locker)
            {
                segments[seg] = otherCell;
            }
            lock (otherCell.locker)
            {
                otherCell.segments[seg] = this;
                /*
                if (this.PointRanks != null)
                {
                    var id1 = Array.IndexOf(this.PointRanks, seg.Source.PointAId);
                    var id2 = Array.IndexOf(this.PointRanks, seg.Source.PointBId);
                    //if (this.InducedLabledVector[id1] == this.InducedLabledVector[id2])
                        //throw new Exception("bug");
                    otherCell.InducedLabledVector = InducedLabledVector.ToArray();
                    Generics.Swap(ref otherCell.InducedLabledVector[id1], ref otherCell.InducedLabledVector[id2]);
                }
                */
            }
        }

        public List<KeyValuePair<LineSegment, Cell>> GetNeighbors()
        {
            lock (locker)
            {
                var res = segments.ToList();
                return res;
            }
        }

        public List<LineSegment> GetCellWalls()
        {
            lock (locker)
            {
                var res = segments.Keys.ToList();
                return res;
            }
        }

        //We will filter neighbor cells which only lower 1's in the vector.
        public bool ImprovesWhenCrossingToNeighbor(LineSegment seg) 
        {
            //what is the min-rank of the points which change places when crossing between these neighbors
            var rankFirst = PointRanks[seg.Source.PointAId];
            var rankSecond = PointRanks[seg.Source.PointBId];
            return false;
        }

        public void SaveToCSV(string file, bool wait=false)
        {
            var segCoords = new List<Coordinate>() { mHG != null ? new Coordinate(mHG.Item1, mHG.Item2) : new Coordinate(-1, -1) };
            segCoords.AddRange(segmentOrder.SelectMany(t => t.GetCoordinate()));
            Generics.SaveToCSV(segCoords, file, wait);
        }

        public static void SaveToCSV(IEnumerable<LineSegment> segs, string file, bool wait = true)
        {
            var segCoords = new List<Coordinate>() { new Coordinate(-1, -1) };
            segCoords.AddRange(segs.SelectMany(t => t.GetCoordinate()));
            Generics.SaveToCSV(segCoords, file, wait);
        }

        public override string ToString()
        {
            return string.Join("->", segments.Keys);
        }

        public override bool Equals(object obj)
        {
            return ((Cell)obj)._cOm.Equals(this._cOm);
        }

        public override int GetHashCode()
        {
            int hc = 0;
            if (segments != null)
                foreach (var p in segments.Keys)
                {
                    hc ^= p.GetHashCode();
                    //hc = (hc << 7) | (hc >> (32 - 7)); //rotale hc to the left to swipe over all bits
                }
            return hc;
        }

        public double MeanSquareRoot()
        {
            var segCoords = this.Coordinates.ToList();
            var meanCoord = new Coordinate(segCoords.Select(c => c.X).Average(), segCoords.Select(c => c.Y).Average());
            return Math.Sqrt(segCoords.Average(c => Math.Pow(c.EuclideanDistance(meanCoord), 2)));
        }

        public double SurfaceArea()
        {
            //This is according to a variation of Green's theorem (https://gist.github.com/listochkin/1200393)
            var area = 0.0;
            var segCoords = this.Coordinates.ToList();
            var N = segCoords.Count;
            for (var i = 0; i < N; i++)
            {
                var j = (i + 1)%N;
                area += segCoords[i].X*segCoords[j].Y - segCoords[i].Y*segCoords[j].X;
            }
            return Math.Abs(area/2.0);
        }

        public double SurfaceAreaSimple()
        {
            //Chop every 3 consecutive pts to triangles, remove center point.
            List<Coordinate> segCoords;
            lock (locker)
            {
                segCoords = this.Coordinates.ToList();
            }
            var N = segCoords.Count;
            var area = 0.0;
            while (segCoords.Count > 2)
            {
                area += GeometryHelpers.AreaOfTriangle(segCoords.Take(3).ToList());
                segCoords.RemoveAt(1);
            }
            return area;
        }

        public void AssignRanking(List<Coordinate> sortedPoints)
        {
            //PointRanks = sortedPoints.Select(t=>);
        }

        public void ComputeRanking(List<ICoordinate> points, bool[] pointLabels, List<string> identities = null, PrincipalComponentAnalysis pca = null)
        {
            var mapping = new Dictionary<ICoordinate, Tuple<int, bool, double, string>>(); //original idx, 1/0 label, distance, string name (for debugging)
            ICoordinate remappedCenter;
            if (pca != null)
            {
                var reverted = pca.Revert(new[] { new[] { CenterOfMass.X, CenterOfMass.Y } });
                remappedCenter = new Coordinate3D(reverted[0][0], reverted[0][1], reverted[0][2]);
            }
            else
                remappedCenter = CenterOfMass;
            for (var i = 0; i < points.Count; i++)
            {
                mapping.Add(points[i],
                    new Tuple<int, bool, double, string>(i, pointLabels[i], points[i].EuclideanDistance(remappedCenter),
                        identities != null ? identities[i] : ""));
            }
            var rankedMap = mapping.OrderBy(pt => pt.Value.Item3).ToList();
            PointRanks = rankedMap
                    .Select((pt, idx) => new {id = pt.Value.Item1, rank = idx})
                    .OrderBy(t => t.id).Select(t => t.rank)
                    .ToArray();
            var namedLabelVector = rankedMap.Select(pt => pt.Value.Item4).ToArray();
            InducedLabledVector = rankedMap.Select(pt => pt.Value.Item2).ToArray();
        }

        /// <summary>
        /// Checks if the cell contains this coordinate
        /// </summary>
        /// <param name="coord"></param>
        /// <returns></returns>
        public bool ContainsCoord(Coordinate coord)
        {
            var crosses = 0;
            foreach (var polyside in this.segments.Keys)
            {
                if (polyside.CrossesY(coord.Y) && polyside.Source.EvaluateAtY(coord.Y)<coord.X)
                    crosses++;
            }
            var contained = crosses%2 == 1;
            return contained;
        }

        public Tesselation.SegmentCellCovered GetSegmentCover(LineSegment seg)
        {
            if(!this.segments.ContainsKey(seg))
                return Tesselation.SegmentCellCovered.None;
            //seg.Source.
            return Tesselation.SegmentCellCovered.None;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                segments.Clear(); //Maps a segment to its cell neighbor
                segments=null;
                segmentOrder.Clear();
                segmentOrder=null;
                _cOm = null;
                _mHG = null;
                InducedLabledVector = null;
                PointRanks = null;
            }
        }

        public double FurthestPointDistance(Coordinate coord)
        {
            return this.Coordinates.Max(t => t.EuclideanDistance(coord));
        }

        public void ReestablishRank(List<Coordinate> points)
        {
            this.PointRanks =
                points.Select((p, id) => new {Id = id, Distance = p.EuclideanDistance(this.CenterOfMass)})
                    .OrderBy(t => t.Distance)
                    .Select(p => p.Id)
                    .ToArray();
        }

        public Tuple<double, int, int[]> Compute_mHG(mHGCorrectionType correctionType, ConfigParams Conf)
        {
            lock (locker)
            {
                _mHG = mHGJumper.minimumHypergeometric(InducedLabledVector, -1, -1, correctionType);
                Conf.mHGlist.Add(new Tuple<double, int>(mHG.Item1, Interlocked.Increment(ref Conf.computedMHGs)));
            }
            return _mHG;
        }

        public void SetId(int id)
        {
            this.MyId = id;
        }

        
    }

    
    public static class GeometryHelpers
    {
        public static double Sigma = 1; //Gaussian needs to decay (exponentially) in the relevant range between points.
        public static double AreaOfTriangle(List<Coordinate> coords)
        {
            return
                Math.Abs(coords[0].X*(coords[1].Y - coords[2].Y) + coords[1].X*(coords[2].Y - coords[0].Y) +
                         coords[2].X*(coords[0].Y - coords[1].Y))/2.0;
        }

        public static double ComputeGaussianDensity(Coordinate a, Coordinate b)
        {
            return ComputeGaussianDensity(a.X, a.Y, b.X, b.Y);
        }

        public static double ComputeGaussianDensity(double x, double y, double x0, double y0)
        {
            var res = Math.Exp(-((x - x0) * (x - x0) + (y - y0) * (y - y0)) / (2 * Sigma * Sigma));

            if (double.IsNaN(res))
            {
                throw new Exception("problem");
            }
            return res;
        }
    }

}

