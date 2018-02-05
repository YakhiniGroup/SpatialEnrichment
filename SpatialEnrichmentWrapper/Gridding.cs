using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SpatialEnrichment;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using Accord.Math;
using SpatialEnrichment.Helpers;
using System.Collections;

namespace SpatialEnrichmentWrapper
{
    public enum GridType { Empirical, Uniform }
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

        public Tuple<ICoordinate, double, long> EvaluateDataset(List<Tuple<ICoordinate, bool>> dataset, int parallelization = 20, string debug=null)
        {
            var smph = new SemaphoreSlim(parallelization);
            var tsks = new List<Task>();
            ICoordinate best = null;
            var pval = 1.0;
            long iterfound = -1, curriter = 0;
            object locker = new object();
            mHGJumper.optHGT = 1;
            StreamWriter outfile = null;
            if (debug != null)
                outfile = new StreamWriter(debug);
            
            foreach (var pivot in GetPivots())
            {
                smph.Wait();
                tsks.RemoveAll(t => t.IsCompleted);
                tsks.Add(Task.Run(() => {
                    var binvec = dataset.OrderBy(c => pivot.EuclideanDistance(c.Item1)).Select(c => c.Item2).ToList();
                    var res = mHGJumper.minimumHypergeometric(binvec);
                    Interlocked.Increment(ref curriter);
                    
                    lock (locker)
                    {
                        if (res.Item1 < pval)
                        {
                            best = pivot;
                            pval = res.Item1;
                            iterfound = curriter;
                        }
                    }
                    smph.Release();
                }));
                if (debug != null)
                    outfile.WriteLine(pivot);
            }
            if(debug != null) outfile.Close();
            Task.WaitAll(tsks.ToArray());
            return new Tuple<ICoordinate, double, long>(best, pval, iterfound);
        }

        public void GenerateBeadPivots(List<Tuple<ICoordinate, bool>> dataset)
        {
            producer = Task.Run(() => {
                foreach (var p in dataset.Select(p => p.Item1))
                    Pivots.Add(p);
                NumPivots = dataset.Count;
                Pivots.CompleteAdding();
            });
        }

        public void GenerateEmpricialDensityGrid(long numsamples, List<Tuple<ICoordinate,bool>> lableddata, double jitterscale = 1E-7, bool inorder = false, string debug=null)
        {
            producer = Task.Run(() =>
            {
                var pairs = new List<Tuple<ICoordinate, ICoordinate>>();
                for (var i = 0; i < lableddata.Count-1; i++)
                for (var j = i+1; j < lableddata.Count; j++)
                    if (lableddata[i].Item2 != lableddata[j].Item2)
                        pairs.Add(new Tuple<ICoordinate, ICoordinate>(lableddata[i].Item1, lableddata[j].Item1));

                var problemDim = lableddata.First().Item1.GetDimensionality();
                var bisectors = pairs.AsParallel().Select(pair => problemDim == 2
                    ? (Hyperplane)Line.Bisector((Coordinate)pair.Item1, (Coordinate)pair.Item2, isCounted: false)
                    : (Hyperplane)Plane.Bisector((Coordinate3D)pair.Item1, (Coordinate3D)pair.Item2)).ToList();
                //bisectors.ForEach(b => ((Plane)b).ToCsv("plane" + Guid.NewGuid() + ".csv")); //debug save planes
                //add boundaries at +- 20 (assumes data is whitened)
                switch (problemDim)
                {
                    case 2:
                        bisectors.Add(new Line(jitterscale, -20, false));
                        break;
                    case 3:
                        bisectors.Add(new Plane(0, 1, 0, 20));
                        break;
                }
                
                //set inorder=true for enumeration without replacement of possible combinations.
                if (inorder)
                {
                    //Lazily generate all (upto ~numsamples) permutations of bisectors pairs (2D) or triplets (3D)
                    var exhaustiveGroups = bisectors.DifferentCombinations(problemDim);
                    Parallel.ForEach(exhaustiveGroups, new ParallelOptions() { MaxDegreeOfParallelism = 2 }, (inducerLst, loopState) =>
                    {
                        var inducers = inducerLst.ToList();
                        var jitteredPivots = GetPivotForCoordSet(inducers, jitterscale);
                        foreach(var piv in jitteredPivots)
                            Pivots.Add(piv);
                        if (Interlocked.Increment(ref NumPivots) >= numsamples)
                            loopState.Stop();
                    });
                }
                else
                {
                    Parallel.For(0, numsamples, new ParallelOptions() { MaxDegreeOfParallelism = 2 }, (i, loopState) =>
                    {
                        var inducers = bisectors.OrderBy(v => StaticConfigParams.rnd.NextDouble()).Take(problemDim).ToList();
                        var jitteredPivots = GetPivotForCoordSet(inducers, jitterscale);
                        foreach (var piv in jitteredPivots)
                            Pivots.Add(piv);
                        if (Interlocked.Increment(ref NumPivots) >= numsamples)
                            loopState.Stop();
                    });
                }
                
                Pivots.CompleteAdding();
            });
        }

        public static IEnumerable<ICoordinate> GetPivotForCoordSet(List<Hyperplane> inducers, double jitterscale = 1E-7)
        {
            var problemDim = inducers.Count;
            ICoordinate intersectionCoord, firstCoord, secondCoord;
            ICoordinate jitteredPivot = null;
            switch (problemDim)
            {
                case 2:
                    var firstbisectorLine = (Line) inducers[0];
                    var secondbisectorLine = (Line) inducers[1];
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
                    yield return jitteredPivot;
                    break;
                case 3:
                    var firstbisectorPlane = (Plane) inducers[0];
                    var secondbisectorPlane = (Plane) inducers[1];
                    var thirdbisectorPlane = (Plane) inducers[2];
                    //We find the intersection of three planes by solving a system of linear equations.
                    double[,] matrix =
                    {
                        {firstbisectorPlane.Normal.X, firstbisectorPlane.Normal.Y, firstbisectorPlane.Normal.Z},
                        {secondbisectorPlane.Normal.X, secondbisectorPlane.Normal.Y, secondbisectorPlane.Normal.Z},
                        {thirdbisectorPlane.Normal.X, thirdbisectorPlane.Normal.Y, thirdbisectorPlane.Normal.Z}
                    };
                    double[,] rightSide = {{-firstbisectorPlane.D}, {-secondbisectorPlane.D}, {-thirdbisectorPlane.D}};
                    var x = matrix.Solve(rightSide);
                    intersectionCoord = new Coordinate3D(x[0, 0], x[1, 0], x[2, 0]);

                    for(var i=0;i < Math.Pow(2,2); i++)
                    {
                        var signs = new BitArray(new int[] { i });
                        //empirical gradients dy
                        firstCoord = (Coordinate3D)intersectionCoord + firstbisectorPlane.Normal.Scale((signs[0] ? jitterscale : -jitterscale));
                        secondCoord = (Coordinate3D)intersectionCoord + secondbisectorPlane.Normal.Scale((signs[1] ? jitterscale : -jitterscale));
                        ICoordinate thirdCoord = (Coordinate3D)intersectionCoord + thirdbisectorPlane.Normal.Scale((signs[2] ? jitterscale : -jitterscale));
                        jitteredPivot = new Coordinate3D(
                            (firstCoord.GetDimension(0) + secondCoord.GetDimension(0) + thirdCoord.GetDimension(0)) / 3.0,
                            (firstCoord.GetDimension(1) + secondCoord.GetDimension(1) + thirdCoord.GetDimension(1)) / 3.0,
                            (firstCoord.GetDimension(2) + secondCoord.GetDimension(2) + thirdCoord.GetDimension(2)) / 3.0);

                        yield return jitteredPivot;
                    }
                    break;
            }
            
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
