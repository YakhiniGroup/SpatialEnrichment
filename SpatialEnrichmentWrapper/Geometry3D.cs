using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SpatialEnrichment;
using Accord.Statistics.Analysis;
using SpatialEnrichment.Helpers;

namespace SpatialEnrichmentWrapper
{
    public class Coordinate3D : IEquatable<Coordinate3D>, ICoordinate
    {
        public readonly double X, Y, Z;
        public int? CoordId;
        public Coordinate3D(double x, double y, double z)
        {
            if (double.IsNaN(x) || double.IsInfinity(x)
             || double.IsNaN(y) || double.IsInfinity(y)
             || double.IsNaN(z) || double.IsInfinity(z))
                throw new ApplicationException("Bad coordinate values");
            X = x;
            Y = y;
            Z = z;
        }

        public double GetDimension(int dim)
        {
            switch (dim)
            {
                case 0:
                    return X;
                case 1:
                    return Y;
                case 2:
                    return Z;
                default:
                    throw new NotImplementedException("Three dimensional data does not implement get dim >2!");
            }
        }

        public bool Equals(Coordinate3D other)
        {
            return (Math.Abs(this.X - other.X) < StaticConfigParams.TOLERANCE) && 
                   (Math.Abs(this.Y - other.Y) < StaticConfigParams.TOLERANCE) &&
                   (Math.Abs(this.Z - other.Z) < StaticConfigParams.TOLERANCE);
        }

        public override int GetHashCode()
        {
            return Convert.ToInt32(31 * X + 17 * Y + 9 * Z);
        }

        public override string ToString()
        {
            return ToString(@"0.00000000");
        }

        public string ToString(string fmt)
        {
            return X.ToString(fmt) + "," + Y.ToString(fmt) + "," + Z.ToString(fmt);
        }

        public static Coordinate3D operator +(Coordinate3D curr, Coordinate3D other)
        {
            return new Coordinate3D(curr.X + other.X, curr.Y + other.Y, curr.Z + other.Z);
        }

        public static Coordinate3D operator -(Coordinate3D curr, Coordinate3D other)
        {
            return new Coordinate3D(curr.X - other.X, curr.Y - other.Y, curr.Z - other.Z);
        }

        public static bool IsPositiveLexicographicProgress(Coordinate3D from, Coordinate3D to)
        {
            if (from.X < to.X)
                return true;
            if (from.X > to.X)
                return false;
            if (from.Y < to.Y)
                return true;
            if (from.Y > to.Y)
                return false;
            if (from.Z < to.Z)
                return true;
            return false;
        }

        public double[] ToArray()
        {
            return new[] { X, Y, Z };
        }

        public double Angle(Coordinate3D other)
        {
            throw new NotImplementedException();
            return Math.Atan2(other.Y, other.X) - Math.Atan2(this.Y, this.X);
        }

        public double DotProduct(Coordinate3D other)
        {
            return (this.X * other.X + this.Y * other.Y + this.Z * other.Z);
        }

        internal double DistanceToPlane(Plane p)
        {
            var denom = Math.Abs(p.Normal.DotProduct(this) + p.D);
            var numer = p.Normal.Norm();
            return denom / numer;
        }

        private double Norm()
        {
            return Math.Sqrt(X * X + Y * Y + Z * Z);
        }

        public double CrossProduct(Coordinate3D other)
        {
            throw new NotImplementedException();
            return (this.X * other.Y - this.Y * other.X);
        }

        public double EuclideanDistance(ICoordinate other)
        {
            return Math.Sqrt(Math.Pow(this.X - ((Coordinate3D)other).X, 2) + 
                             Math.Pow(this.Y - ((Coordinate3D)other).Y, 2) +
                             Math.Pow(this.Z - ((Coordinate3D)other).Z, 2));
        }

        public static ICoordinate MakeRandom()
        {
            return new Coordinate3D(StaticConfigParams.rnd.NextDouble(), 
                                    StaticConfigParams.rnd.NextDouble(),
                                    StaticConfigParams.rnd.NextDouble());
        }

        internal Coordinate3D Scale(double s)
        {
            return new Coordinate3D(s * X, s * Y, s * Z);
        }
    }


    public class Plane
    {
        public int PointAId = -1, PointBId = -1;
        public readonly int Id;
        public Coordinate3D Normal, MidPoint;
        public double D; //plane given as aX+bY+cZ=d where a,b,c is the Normal

        public Plane(double a, double b, double c, double d)
        {
            Normal = new Coordinate3D(a,b,c);
            D = d;
        }

        public static Plane Bisector(Coordinate3D a, Coordinate3D b)
        {
            var normalVec = b - a;
            var midPoints = new Coordinate3D((a.X + b.X) / 2.0, (a.Y + b.Y) / 2.0, (a.Z + b.Z) / 2.0);
            var d = normalVec.DotProduct(midPoints);
            return new Plane(normalVec.X * midPoints.X, normalVec.Y * midPoints.Y, normalVec.Z * midPoints.Z, d)
                       { MidPoint = midPoints };
        }

        public Coordinate3D ProjectOnto(Coordinate3D coord)
        {
            var norm = Math.Sqrt(Normal.X * Normal.X + Normal.Y * Normal.Y + Normal.Z * Normal.Z);
            var uNormal = Normal.Scale(1.0 / norm);
            return coord - uNormal.Scale((coord - MidPoint).DotProduct(uNormal));
            //new Coordinate3D(coord.X * MidPoint.X, coord.Y * MidPoint.Y, coord.Z * MidPoint.Z);
            //var dist = v.DotProduct(uNormal);
            //return coord - uNormal.Scale(dist);
        }

        public List<Coordinate> ProjectOntoAndRotate(List<Coordinate3D> coords, out PrincipalComponentAnalysis pca)
        {
            //Project all coordinates to plane
            var projList = coords.Select(c => ProjectOnto(c)).ToList();
            //Take the colinear points on the plane and compute their PCA
            var sourceMatrix = projList.Select(t => new[] { t.X, t.Y, t.Z }).ToArray();
            pca = new PrincipalComponentAnalysis() { Method = PrincipalComponentMethod.Center, Whiten = false };
            var transform = pca.Learn(sourceMatrix);

            //Use pca for aligning plane to a 2D frame of reference.
            pca.NumberOfOutputs = 2; //project to 2D
            var transformedData = pca.Transform(sourceMatrix);
            return transformedData.Select(p => new Coordinate(p[0], p[1])).ToList();
        }
    }

}
