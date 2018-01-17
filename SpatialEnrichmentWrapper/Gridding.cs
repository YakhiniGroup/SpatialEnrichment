using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SpatialEnrichment;
using System.Collections.Concurrent;
using System.Threading;
using Accord.Math;
using SpatialEnrichment.Helpers;

namespace SpatialEnrichmentWrapper
{
    public class Gridding
    {
        private Task producer;
        private BlockingCollection<ICoordinate> pivots;
        private IEnumerator<ICoordinate> elementEnumerator;
        public ICoordinate NextPivot => elementEnumerator.MoveNext() ? elementEnumerator.Current : null;
        public long NumPivots = 0;
        public Gridding()
        {
            pivots = new BlockingCollection<ICoordinate>(10000);
            elementEnumerator = pivots.GetConsumingEnumerable().GetEnumerator();
        }

        public void GeneratePivotGrid(long numsamples, int dim=2, double buffer=0.1)
        {
            producer = Task.Run(() =>
            {
                var resolution = Math.Pow(numsamples, 1.0/dim);
                for (var i = -buffer; i < 1 + buffer; i += 1.0 / resolution)
                for (var j = -buffer; j < 1 + buffer; j += 1.0 / resolution)
                    switch (dim)
                    {
                        case 2:
                            pivots.Add(new Coordinate(i, j));
                            break;
                        case 3:
                            for (var k = -buffer; k < 1 + buffer; k += 1.0 / resolution)
                                pivots.Add(new Coordinate3D(i, j, k));
                            break;
                    }
                
                pivots.CompleteAdding();
            });
        }

        public void GenerateEmpricialDensityGrid(long numsamples, List<Tuple<ICoordinate,bool>> lableddata, double jitterscale = 1E-7)
        {
            producer = Task.Run(() =>
            {
                var pairs = new List<Tuple<ICoordinate, ICoordinate>>();
                for (var i = 0; i < lableddata.Count-1; i++)
                for (var j = i+1; j < lableddata.Count; j++)
                    if (lableddata[i].Item2 != lableddata[j].Item2)
                        pairs.Add(new Tuple<ICoordinate, ICoordinate>(lableddata[i].Item1, lableddata[j].Item1));
                //add bottom boundary
                var problemDim = lableddata.First().Item1.GetDimensionality();
                switch (problemDim)
                {
                    case 2:
                        pairs.Add(new Tuple<ICoordinate, ICoordinate>(new Coordinate(0, 0), new Coordinate(jitterscale, -20)));
                        break;
                    case 3:
                        pairs.Add(new Tuple<ICoordinate, ICoordinate>(new Coordinate3D(0, 0, 0), new Coordinate3D(jitterscale, -20, jitterscale)));
                        break;
                }

                var exhaustiveGroups = pairs.DifferentCombinations(problemDim);
                Parallel.ForEach(exhaustiveGroups, (inducerLst, loopState) =>
                {
                    
                    var inducers = inducerLst.ToList();
                    ICoordinate intersectionCoord, first_coord, second_coord, third_coord, jitteredPivot = null;
                    switch (problemDim)
                    {
                        case 2:
                            var firstbisectorLine = Line.Bisector((Coordinate)inducers[0].Item1, (Coordinate)inducers[0].Item2, isCounted: false);
                            var secondbisectorLine = Line.Bisector((Coordinate)inducers[1].Item1, (Coordinate)inducers[1].Item2, isCounted: false);
                            intersectionCoord = firstbisectorLine.Intersection(secondbisectorLine);
                            //empirical gradient
                            var first_posdir = firstbisectorLine.EvaluateAtX(intersectionCoord.GetDimension(0) + jitterscale);
                            var first_negdir = firstbisectorLine.EvaluateAtX(intersectionCoord.GetDimension(0) - jitterscale);
                            var second_posdir = secondbisectorLine.EvaluateAtX(intersectionCoord.GetDimension(0) + jitterscale);
                            var second_negdir = secondbisectorLine.EvaluateAtX(intersectionCoord.GetDimension(0) - jitterscale);
                            //Find local minima directions.
                            first_coord = first_posdir > first_negdir ?
                                new Coordinate(intersectionCoord.GetDimension(0) + jitterscale, first_posdir) :
                                new Coordinate(intersectionCoord.GetDimension(0) - jitterscale, first_negdir);
                            second_coord = second_posdir > second_negdir ?
                                new Coordinate(intersectionCoord.GetDimension(0) + jitterscale, second_posdir) :
                                new Coordinate(intersectionCoord.GetDimension(0) - jitterscale, second_negdir);
                            //averaging ensures we are in the exact cell who's bottom-most coordinate is the intersection coord.
                            jitteredPivot = new Coordinate(first_coord.GetDimension(0) + second_coord.GetDimension(0) / 2.0, first_coord.GetDimension(1) + second_coord.GetDimension(1) / 2.0);
                            /*
                            // Note: I abandoned the Random sampled jitter strategy since this simpler strategy ensures a 1-1 mapping between an intersectionCoord and a cell (up to scale).
                            var jitteredPivot = new Coordinate(
                                intersectionCoord.X + GeometryHelpers.SampleGaussian(StaticConfigParams.rnd, 0.0, jitterscale),
                                intersectionCoord.Y + GeometryHelpers.SampleGaussian(StaticConfigParams.rnd, 0.0, jitterscale));
                            */
                            break;
                        case 3:
                            var firstbisectorPlane = Plane.Bisector((Coordinate3D)inducers[0].Item1, (Coordinate3D)inducers[0].Item2);
                            var secondbisectorPlane = Plane.Bisector((Coordinate3D)inducers[1].Item1, (Coordinate3D)inducers[1].Item2);
                            var thirdbisectorPlane = Plane.Bisector((Coordinate3D)inducers[2].Item1, (Coordinate3D)inducers[2].Item2);
                            //We find the intersection of three planes by solving a system of linear equations.
                            double[,] matrix =
                            {
                                { firstbisectorPlane.Normal.X, firstbisectorPlane.Normal.Y, firstbisectorPlane.Normal.Z },
                                { secondbisectorPlane.Normal.X, secondbisectorPlane.Normal.Y, secondbisectorPlane.Normal.Z },
                                { thirdbisectorPlane.Normal.X, thirdbisectorPlane.Normal.Y, thirdbisectorPlane.Normal.Z }
                            };
                            double[,] rightSide = { { -firstbisectorPlane.D }, { -secondbisectorPlane.D }, { -thirdbisectorPlane.D } };
                            var x = Matrix.Solve(matrix, rightSide, leastSquares: true);
                            intersectionCoord = new Coordinate3D(x[0, 0], x[1, 0], x[2, 0]);
                            //empirical gradients dy
                            first_coord = (Coordinate3D)intersectionCoord + firstbisectorPlane.Normal.Scale(jitterscale);
                            second_coord = (Coordinate3D)intersectionCoord + secondbisectorPlane.Normal.Scale(jitterscale);
                            third_coord = (Coordinate3D)intersectionCoord + thirdbisectorPlane.Normal.Scale(jitterscale);
                            jitteredPivot = new Coordinate3D((first_coord.GetDimension(0) + second_coord.GetDimension(0) + third_coord.GetDimension(0)) / 3.0,
                                                             (first_coord.GetDimension(1) + second_coord.GetDimension(1) + third_coord.GetDimension(1)) / 3.0,
                                                             (first_coord.GetDimension(2) + second_coord.GetDimension(2) + third_coord.GetDimension(2)) / 3.0);
                            break;
                    }
                    pivots.Add(jitteredPivot);
                    if(Interlocked.Increment(ref NumPivots) >= numsamples)
                        loopState.Stop();
                });

                /*
                Parallel.For(0, numsamples, (i) =>
                {
                    var inducers = pairs.OrderBy(v => StaticConfigParams.rnd.NextDouble()).Take(problemDim).ToList();
                });
                */
                pivots.CompleteAdding();
            });
        }

        public IEnumerable<ICoordinate> GetPivots()
        {
            foreach (var el in pivots.GetConsumingEnumerable())
                yield return el;
        }

    }
}
