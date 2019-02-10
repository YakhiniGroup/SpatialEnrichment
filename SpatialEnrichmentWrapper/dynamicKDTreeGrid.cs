using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SpatialEnrichment.Helpers;

namespace SpatialEnrichmentWrapper
{

    public class SpaceCube : Coordinate3D, IDisposable
    {
        private readonly double minX, maxX, minY, maxY, minZ, maxZ;

        public double Xrange => (maxX - minX)/2.0;
        public double Yrange => (maxY - minY)/2.0;
        public double Zrange => (maxZ - minZ)/2.0;
        public Coordinate3D GetMidpoint => this;
        public List<Plane> Planes = new List<Plane>();

        public int[] MinCellsToOpt;


        public SpaceCube(double x0, double x1, double y0, double y1, double z0, double z1) : base(x0 + (x1 - x0) / 2.0,
            y0 + (y1 - y0) / 2.0, z0 + (z1 - z0) / 2.0)
        {
            minX = x0;
            maxX = x1;
            minY = y0;
            maxY = y1;
            minZ = z0;
            maxZ = z1;
        }

        public Coordinate3D[] GetCorners()
        {
            var res= new Coordinate3D[8];
            var a = 0;
            for (var i = 0; i < 2; i++)
            for (var j = 0; j < 2; j++)
            for (var k = 0; k < 2; k++)
                res[a++] = new Coordinate3D(GetDimensionCorner(1, i == 0), 
                    GetDimensionCorner(2, j == 0),
                    GetDimensionCorner(3, k == 0));
            return res;
        }

        private double GetDimensionCorner(int dim, bool minVal)
        {
            switch (dim)
            {
                case 1:
                    return minVal ? minX : maxX;
                case 2:
                    return minVal ? minY : maxY;
                case 3:
                    return minVal ? minZ : maxZ;
                default:
                    return -1;
            }

        }


        //Returns true if all cube corners yield the same data ranking
        public bool AllCornersInSameCell(List<Tuple<ICoordinate, bool>> data)
        {
            return Planes.Count == 0;

            var corners = GetCorners();
            var rankMat = corners.Select(p => data.Select((v,idx)=>new {Value=v, Index=idx}).OrderBy(v => v.Value.Item1.EuclideanDistance(p)).Select(v=>v.Index).GetEnumerator()).ToArray();
            for (var i = 0; i < data.Count; i++)
            {
                rankMat[0].MoveNext();
                for (var j = 1; j < 8; j++)
                {
                    rankMat[j].MoveNext();
                    if (rankMat[j].Current != rankMat[0].Current)
                        return false;
                }
            }
            return true;
        }

        //Parcels space into its eight subcubes
        public SpaceCube[] GetSubCubes()
        {
            //var distinctCorners = new HashSet<Coordinate3D>();
            var res = new SpaceCube[8];
            var a = 0;
            for (var i = 0; i < 2; i++)
                for (var j = 0; j < 2; j++)
                    for (var k = 0; k < 2; k++)
                    {
                        res[a++] = new SpaceCube(minX + i * Xrange, maxX - (1 - i) * Xrange,
                            minY + j * Yrange, maxY - (1 - j) * Yrange,
                            minZ + k * Zrange, maxZ - (1 - k) * Zrange);
                        foreach(var plane in Planes)
                            if (res[a-1].IntersectsPlane(plane))
                                res[a-1].Planes.Add(plane);
                    }
            return res;
        }

        public bool IntersectsPlane(Plane that)
        {
            var cornersigns = GetCorners().Select(that.GetSignOfCoord).ToArray();
            return !cornersigns.All(v => v) && cornersigns.Any(v => v);
        }

        public bool ContainsCoordinate(Coordinate3D that)
        {
            return ((that.X >= this.minX && that.X <= this.maxX) &&
                    (that.Y >= this.minY && that.Y <= this.maxY) &&
                    (that.Z >= this.minZ && that.Z <= this.maxZ));
        }


        public void Dispose()
        {
            Planes.Clear();
        }

        public void WaitForSkipsArray(TimeSpan timeout)
        {
            var startWait = DateTime.Now;
            while (MinCellsToOpt == null && DateTime.Now - startWait < timeout)
            {
                Thread.Sleep(200);
            }
        }

        /// <summary>
        /// Consider tesselation cells inside the cube as a graph with N nodes 
        /// Q: What is the maximal distance from the pivot to the edge of the cube (how many cells can we cross inside the cube)?
        /// A: Root at pivot's cell, we want the least amount of neighbors per cell in order to have a deep tree.
        ///    Since cells are convex the deepest possible tree will be a 4-regular graph (requires proof, but imagine pyramids).
        ///    diameter of a 4-regular graph is O(N/4) (https://math.stackexchange.com/questions/351945/diameter-of-k-regular-graph). 
        ///    Thus if we require > L steps to reach a new OPT, we need only inspect cubes where ~ N > O(4*L).
        ///    Alternatively, say N = O(4*L) + o, we can look up pivot's HGTMat[o] to provide a bound on the minimal possible p-value.
        ///    Presumably, if o >> B, we are saturated, thus we would want to minimize o.
        /// </summary>
        /// <returns></returns>
        public double EstimateCellCount()
        {
            //var minCellCount = Planes.Count + 1; //i.e. planes do not intersect eachother inside the cube, the space of the cube is divided like so.
            if (Planes.Count < 2)
                return Planes.Count + 1;

            if (Planes.Count == 2)
                return 4; //its 3 if parallel, we overestimate.

            var maxCellCount = MathExtensions.NChooose3(Planes.Count); //i.e. every 3 planes generate an intersection in the cube.
            //if (Planes.Count > 10)
            return maxCellCount;

            /*
            var intrscts = 0;
            foreach (var inducers in Planes.DifferentCombinations(3).Select(g => g.Select(p=> (Hyperplane)p).ToList()))
                foreach(var pt in Gridding.GetPivotForCoordSet(inducers))
                    if (ContainsCoordinate((Coordinate3D) pt))
                        intrscts++;

            return intrscts;
            */
        }

        public int CountBisectorsOnNegSide()
        {
            var rescount = 0;
            foreach (var p in Planes)
            {
                rescount += p.GetSignOfCoord(this) != p.signOfPosLbl ? 1 : 0;
            }
            return rescount;
        }

    }
}
