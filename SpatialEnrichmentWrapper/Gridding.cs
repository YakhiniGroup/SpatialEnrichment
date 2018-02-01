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
        public BlockingCollection<ICoordinate> Pivots;
        private IEnumerator<ICoordinate> elementEnumerator;
        public ICoordinate NextPivot => elementEnumerator.MoveNext() ? elementEnumerator.Current : null;
        public long NumPivots = 0;
        public Gridding()
        {
            Pivots = new BlockingCollection<ICoordinate>(5000);
            elementEnumerator = Pivots.GetConsumingEnumerable().GetEnumerator();
        }

        public void ReturnPivots(IEnumerable<ICoordinate> data)
        {
            foreach (var coordinate in data)
            {
                Pivots.Add(coordinate);
            }
            Pivots.CompleteAdding();
        }

        public void GeneratePivotGrid(long numsamples, int dim=2, double buffer=0.1)
        {
            //todo implement some sort of diagonaization to enumerate with increasing resolution indefinetly
            producer = Task.Run(() =>
            {
                var resolution = Math.Pow(numsamples, 1.0/dim);
                for (var i = -buffer; i < 1 + buffer; i += 1.0 / resolution)
                for (var j = -buffer; j < 1 + buffer; j += 1.0 / resolution)
                    switch (dim)
                    {
                        case 2:
                            Pivots.Add(new Coordinate(i, j));
                            break;
                        case 3:
                            for (var k = -buffer; k < 1 + buffer; k += 1.0 / resolution)
                                Pivots.Add(new Coordinate3D(i, j, k));
                            break;
                    }
                
                Pivots.CompleteAdding();
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

                //Lazily generate all (upto ~numsamples) permutations of bisectors pairs (2D) or triplets (3D)
                var exhaustiveGroups = pairs.OrderBy(v => StaticConfigParams.rnd.NextDouble())
                    .DifferentCombinations(problemDim);

                Parallel.ForEach(exhaustiveGroups, new ParallelOptions() {MaxDegreeOfParallelism = 2}, (inducerLst, loopState) =>
                {
                    var inducers = inducerLst.ToList();
                    var jitteredPivot = GetPivotForCoordSet(inducers, jitterscale);
                    Pivots.Add(jitteredPivot);
                    if(Interlocked.Increment(ref NumPivots) >= numsamples)
                        loopState.Stop();
                });
                
                Pivots.CompleteAdding();
            });
        }

        public static ICoordinate GetPivotForCoordSet(List<List<Coordinate3D>> inducers, double jitterscale = 1E-7)
        {
            return GetPivotForCoordSet(inducers.Select(p => new Tuple<ICoordinate, ICoordinate>(p[0], p[1])).ToList(), jitterscale);
        }

        public static ICoordinate GetPivotForCoordSet(List<Tuple<ICoordinate, ICoordinate>> inducers, double jitterscale = 1E-7)
        {
            var problemDim = inducers.Count;
            ICoordinate intersectionCoord, firstCoord, secondCoord;
            ICoordinate jitteredPivot = null;
            switch (problemDim)
            {
                case 2:
                    var firstbisectorLine = Line.Bisector((Coordinate) inducers[0].Item1, (Coordinate) inducers[0].Item2,
                        isCounted: false);
                    var secondbisectorLine = Line.Bisector((Coordinate) inducers[1].Item1, (Coordinate) inducers[1].Item2,
                        isCounted: false);
                    intersectionCoord = firstbisectorLine.Intersection(secondbisectorLine);
                    //empirical gradient
                    var first_posdir = firstbisectorLine.EvaluateAtX(intersectionCoord.GetDimension(0) + jitterscale);
                    var first_negdir = firstbisectorLine.EvaluateAtX(intersectionCoord.GetDimension(0) - jitterscale);
                    var second_posdir = secondbisectorLine.EvaluateAtX(intersectionCoord.GetDimension(0) + jitterscale);
                    var second_negdir = secondbisectorLine.EvaluateAtX(intersectionCoord.GetDimension(0) - jitterscale);
                    //Find local minima directions.
                    firstCoord = first_posdir > first_negdir
                        ? new Coordinate(intersectionCoord.GetDimension(0) + jitterscale, first_posdir)
                        : new Coordinate(intersectionCoord.GetDimension(0) - jitterscale, first_negdir);
                    secondCoord = second_posdir > second_negdir
                        ? new Coordinate(intersectionCoord.GetDimension(0) + jitterscale, second_posdir)
                        : new Coordinate(intersectionCoord.GetDimension(0) - jitterscale, second_negdir);
                    //averaging ensures we are in the exact cell who's bottom-most coordinate is the intersection coord.
                    jitteredPivot = new Coordinate(firstCoord.GetDimension(0) + secondCoord.GetDimension(0) / 2.0,
                        firstCoord.GetDimension(1) + secondCoord.GetDimension(1) / 2.0);
                    break;
                case 3:
                    var firstbisectorPlane = Plane.Bisector((Coordinate3D) inducers[0].Item1, (Coordinate3D) inducers[0].Item2);
                    var secondbisectorPlane =
                        Plane.Bisector((Coordinate3D) inducers[1].Item1, (Coordinate3D) inducers[1].Item2);
                    var thirdbisectorPlane = Plane.Bisector((Coordinate3D) inducers[2].Item1, (Coordinate3D) inducers[2].Item2);
                    //We find the intersection of three planes by solving a system of linear equations.
                    double[,] matrix =
                    {
                        {firstbisectorPlane.Normal.X, firstbisectorPlane.Normal.Y, firstbisectorPlane.Normal.Z},
                        {secondbisectorPlane.Normal.X, secondbisectorPlane.Normal.Y, secondbisectorPlane.Normal.Z},
                        {thirdbisectorPlane.Normal.X, thirdbisectorPlane.Normal.Y, thirdbisectorPlane.Normal.Z}
                    };
                    double[,] rightSide = {{-firstbisectorPlane.D}, {-secondbisectorPlane.D}, {-thirdbisectorPlane.D}};
                    var x = matrix.Solve(rightSide, leastSquares: true);
                    intersectionCoord = new Coordinate3D(x[0, 0], x[1, 0], x[2, 0]);
                    //empirical gradients dy
                    firstCoord = (Coordinate3D) intersectionCoord + firstbisectorPlane.Normal.Scale(jitterscale);
                    secondCoord = (Coordinate3D) intersectionCoord + secondbisectorPlane.Normal.Scale(jitterscale);
                    ICoordinate thirdCoord = (Coordinate3D) intersectionCoord + thirdbisectorPlane.Normal.Scale(jitterscale);
                    jitteredPivot = new Coordinate3D(
                        (firstCoord.GetDimension(0) + secondCoord.GetDimension(0) + thirdCoord.GetDimension(0)) / 3.0,
                        (firstCoord.GetDimension(1) + secondCoord.GetDimension(1) + thirdCoord.GetDimension(1)) / 3.0,
                        (firstCoord.GetDimension(2) + secondCoord.GetDimension(2) + thirdCoord.GetDimension(2)) / 3.0);
                    break;
            }
            return jitteredPivot;
        }

        public IEnumerable<ICoordinate> GetPivots()
        {
            foreach (var el in Pivots.GetConsumingEnumerable())
                yield return el;
        }

        public static Tuple<double, int> EvaluatePivot(Coordinate3D p, List<Coordinate3D> coords, List<bool> labels)
        {
            return EvaluatePivot((ICoordinate)p, coords.Cast<ICoordinate>().ToList(), labels);
        }

        public static Tuple<double, int> EvaluatePivot(ICoordinate p, List<ICoordinate> coords, List<bool> labels)
        {
            var data = coords.Zip(labels, (a, b) => new {Coords = a, Labels = b}).OrderBy(c => c.Coords.EuclideanDistance(p)).Select(c => c.Labels).ToList();
            var res = mHGJumper.minimumHypergeometric(data);
            return new Tuple<double, int>(res.Item1,res.Item2);

        }

    }
}
