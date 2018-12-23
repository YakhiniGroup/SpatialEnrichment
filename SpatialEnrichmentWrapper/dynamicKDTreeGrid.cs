using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SpatialEnrichmentWrapper
{

    public class SpaceCube
    {
        private readonly double minX, maxX, minY, maxY, minZ, maxZ;

        public double Xrange => (maxX - minX)/2.0;
        public double Yrange => (maxY - minY)/2.0;
        public double Zrange => (maxZ - minZ)/2.0;
        public Coordinate3D GetMidpoint => new Coordinate3D(minX + Xrange, minY + Yrange, minZ + Zrange);


        public SpaceCube(double x0, double x1, double y0, double y1, double z0, double z1)
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
                            //distinctCorners.Add(corner);
                    }
            return res;
        }
    }
}
