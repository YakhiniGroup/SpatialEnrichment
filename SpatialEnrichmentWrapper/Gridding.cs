﻿using System;
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
        private SamplingType _type;
        private Task producer;
        public BlockingCollection<ICoordinate> Pivots;
        private IEnumerator<ICoordinate> elementEnumerator;
        public ICoordinate NextPivot => elementEnumerator.MoveNext() ? elementEnumerator.Current : null;
        public long NumPivots = 0;
        public long EvaluatedPivots = 0;
        private ICoordinate CurrOptLoci;
        private double CurrOptPval;
        private int CurrOptThresh;

        public System.Timers.Timer timer = new System.Timers.Timer();
        private StreamWriter _timerlog;
        private DateTime _startTime;
        private BlockingCollection<string> _logQueue;
        private ConcurrentBag<Tuple<double, int, int, ICoordinate, long>> pValueHistory;
        public ConcurrentBag<Tuple<double, long>> ProgressList;
        public bool TrackProgress = false;
        private CancellationTokenSource tokenSource = new CancellationTokenSource();
        private Task _logTask;
        private INormalizer nrm;
        private ConcurrentPriorityQueue<int, SpaceCube> candidateCubes;
        public Gridding(SamplingType type, INormalizer norm = null)
        {
            _type = type;
            nrm = norm;
            Pivots = new BlockingCollection<ICoordinate>(5000);
            elementEnumerator = Pivots.GetConsumingEnumerable().GetEnumerator();
        }

        public void StartTimeDebug(string filename, INormalizer cnrm, double interval = 10000)
        {
            nrm = cnrm;
            timer.Interval = interval;
            _timerlog = new StreamWriter(filename);
            timer.Elapsed += Timer_Elapsed;
            _startTime = DateTime.Now;
            _logQueue = new BlockingCollection<string>();
            timer.Enabled = true;
            _logTask = Task.Run(() => {
                foreach(var line in _logQueue.GetConsumingEnumerable())
                    _timerlog.WriteLine(line);
            });
        }

        public void StopTimeDebug()
        {
            timer.Enabled = false;
            _logQueue.CompleteAdding();
            _logTask.Wait();
            _timerlog.Close();
        }

        private void Timer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            _logQueue.Add($"{(e.SignalTime - _startTime).TotalSeconds},{EvaluatedPivots},{CurrOptPval},{CurrOptThresh},{nrm.DeNormalize(CurrOptLoci)}");
        }

        public void ReturnPivots(IEnumerable<ICoordinate> data)
        {
            producer = Task.Run(() =>
            {
                foreach (var coordinate in data)
                {
                    Pivots.Add(coordinate);
                }
                Pivots.CompleteAdding();
			});
		}

        public List<Tuple<double,int, int,ICoordinate>> GetQvalues(ConcurrentBag<Tuple<double, int, int, ICoordinate, long>> fromData = null, double qthreshold = 0.05)
        {
            var pVals = fromData ?? pValueHistory;
            var qvals = pVals.Select(v=>v.Item1).ToList().FDRCorrection();
            var res = qvals.Zip(pVals, (a, b) => Tuple.Create(a, b.Item2, b.Item3, b.Item4))
                .OrderBy(v => v.Item1).ThenByDescending(v=>v.Item3/v.Item2)
                .TakeWhile((v,idx) => (v.Item1 < qthreshold || idx < 10) && idx < 100).ToList();
            return res;
        }

        //Generate linearly spaced vector
        public static IEnumerable<double> LinSpace(double from, double to, int k)
        {
            var intervalsize = (to - from) / (k - 1);
            for (var i = 0; i < k; i++)
                yield return from + (i * intervalsize);
        }

        public void GeneratePivotGrid(long numsamples, MinMaxNormalizer nrm, int dim=2, double buffer=0.1)
        {
            //todo implement some sort of diagonaization to enumerate with increasing resolution indefinetly
            producer = Task.Run(() =>
            {
                var resolution = (int)Math.Round(Math.Pow(numsamples, 1.0/dim));
                foreach (var i in LinSpace(nrm.botranges[0] - buffer, nrm.topranges[0] + buffer, resolution))
                foreach (var j in LinSpace(nrm.botranges[1] - buffer, nrm.topranges[1] + buffer, resolution))
                        switch (dim)
                        {
                            case 2:
                                Pivots.Add(new Coordinate(i, j));
                                Interlocked.Increment(ref NumPivots);
                                break;
                            case 3:
                                foreach (var k in LinSpace(nrm.botranges[2] - buffer, nrm.topranges[2] + buffer, resolution))
                                {
                                    Pivots.Add(new Coordinate3D(i, j, k));
                                    Interlocked.Increment(ref NumPivots);
                                }
                                break;
                        }
                
                Pivots.CompleteAdding();
            });
        }

        public void GenerateRecrusivePivotGrid(List<Tuple<ICoordinate,bool>> data, double buffer = 0.1)
        {
            
            var ranges = new MinMaxNormalizer(data.Select(v => v.Item1).ToList());
            candidateCubes = new ConcurrentPriorityQueue<int, SpaceCube>();
            producer = Task.Run(() =>
            {
                var planes = data.DifferentCombinations(2).AsParallel().Select(v => v.ToList())
                    .Where(p => p[0].Item2 != p[1].Item2).Select(pair =>
                        Plane.Bisector((Coordinate3D) pair[0].Item1, (Coordinate3D) pair[1].Item1)).ToList();

                candidateCubes.Enqueue(0,
                    new SpaceCube(ranges.botranges[0] - buffer * ranges.denom[0],
                                  ranges.topranges[0] + buffer * ranges.denom[0],
                                  ranges.botranges[1] - buffer * ranges.denom[1],
                                  ranges.topranges[1] + buffer * ranges.denom[1],
                                  ranges.botranges[2] - buffer * ranges.denom[2],
                                  ranges.topranges[2] + buffer * ranges.denom[2])
                        { Planes = planes }
                    );
                var QueueConsumers = new List<Task>();
                while (candidateCubes.TryDequeue(out var curr))
                {
                    Pivots.Add(curr.Value);

                    if (curr.Value.AllCornersInSameCell(data))
                        continue;

                    if (QueueConsumers.Count > 20)
                    {
                        var tid = Task.WaitAny(QueueConsumers.ToArray());
                        QueueConsumers.RemoveAt(tid);
                    }

                    var currLocal = curr;
                    QueueConsumers.Add(Task.Run(() =>
                    {
                        var estcount = currLocal.Value.EstimateCellCount();
                        if (estcount <= mHGJumper.Ones + 1)
                        {
                            currLocal.Value.WaitForSkipsArray(TimeSpan.FromMinutes(1));
                            if (currLocal.Value.MinCellsToOpt != null &&
                                currLocal.Value.MinCellsToOpt.Min(v => v) > (estcount - 1))
                                return;
                        }
                        foreach (var sub in currLocal.Value.GetSubCubes())
                        {
                            candidateCubes.Enqueue(currLocal.Key + 1, sub);
                        }
                    }));
                    while(candidateCubes.IsEmpty && QueueConsumers.Count > 0 && QueueConsumers.Any(t=>t.Status != TaskStatus.RanToCompletion))
                        Thread.Sleep(200);
                    if (tokenSource.Token.IsCancellationRequested)
                        return;

                }
                Pivots.CompleteAdding();
            }, tokenSource.Token);
        }


        public Tuple<ICoordinate, double, int, int, long> EvaluateDataset(List<Tuple<ICoordinate, bool>> dataset, int parallelization = 10, string debug=null, TimeSpan? maxDuration = null, bool consoleDbg=false, bool trackAll=false)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            //var left = Console.CursorLeft;
            //var top = Console.CursorTop;
            var tsks = new List<Task>();
            CurrOptLoci = null;
            CurrOptPval = 1.1;
            CurrOptThresh = -1;
            int smallBinOptThresh = -1;
            long iterfound = -1;
            EvaluatedPivots = 0;
            object locker = new object();
            mHGJumper.optHGT = 1;
            StreamWriter outfile = null;
            if (trackAll) pValueHistory = new ConcurrentBag<Tuple<double,int, int,ICoordinate, long>>();
            if (TrackProgress) ProgressList = new ConcurrentBag<Tuple<double, long>>();
            if (debug != null)
                outfile = new StreamWriter(debug);
            var celltracker = new ConcurrentDictionary<BitArray,bool>();
            for (var i = 0; i < parallelization; i++)
                tsks.Add(Task.Run(() => {
                    foreach (var pivot in GetPivots())
                    {
                        Tuple<double, int, int[]> res = null;
                        var binvec = dataset.OrderBy(c => c.Item1.EuclideanDistance(pivot)).Select(c => c.Item2).ToList();
                        if (_type == SamplingType.Grid)
                        {
                            var asbitarray = new BitArray(binvec.ToArray());
                            if (!celltracker.TryAdd(asbitarray, true))
                            {
                                Console.Write('.');
                                continue;
                            }
                        }
                        res = mHGJumper.minimumHypergeometric(binvec);
                        if (_type == SamplingType.RecursiveGrid)
                            ((SpaceCube) pivot).MinCellsToOpt = res.Item3;
                        var smallB = binvec.Take(res.Item2).Count(v => v);
                        var iter = Interlocked.Increment(ref EvaluatedPivots);
                        if (trackAll) pValueHistory.Add(Tuple.Create(res.Item1, res.Item2, smallB, pivot, iter));
                        lock (locker)
                        {
                            if (res.Item1 < CurrOptPval ||
                                (Math.Abs(res.Item1 - CurrOptPval) < StaticConfigParams.TOLERANCE &&
                                 smallB / res.Item2 > smallBinOptThresh / CurrOptThresh))
                            {
                                CurrOptLoci = nrm != null ? nrm.DeNormalize(pivot) : pivot;
                                CurrOptPval = res.Item1;
                                CurrOptThresh = res.Item2;
                                smallBinOptThresh = smallB;
                                iterfound = iter;
                                if (TrackProgress) ProgressList.Add(Tuple.Create(CurrOptPval, iterfound));
                            }
                        }
                        if (debug != null)
                            outfile.WriteLine(nrm != null ? nrm.DeNormalize(pivot) : pivot);
                        if (maxDuration.HasValue && sw.Elapsed > maxDuration.Value)
                            return;
                        if (consoleDbg && EvaluatedPivots % 10000 == 0)
                        {
                            Console.Write($"\r\r\r\r\r\r\r\r\r\r\r\r\r\r\r\r\r\r\r\rPivot #(computed/observed): {EvaluatedPivots:N0}/" +
                           $"{NumPivots:N0}. Curr mHG={CurrOptPval}. Thresh={CurrOptThresh}. Bonferroni={CurrOptPval * EvaluatedPivots}. Position:{CurrOptLoci.ToString(@"0.00")}");
                        }
                    }
                }));
            
            Task.WaitAll(tsks.ToArray());
            tokenSource.Cancel();
            if (debug != null) outfile.Close();
            celltracker.Clear();
            if (TrackProgress) ProgressList.Add(Tuple.Create(CurrOptPval, EvaluatedPivots));
            return new Tuple<ICoordinate, double, int, int, long>(CurrOptLoci, CurrOptPval, CurrOptThresh, smallBinOptThresh, iterfound);
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

        public void GenerateEmpricialDensityGrid(long numsamples, List<Tuple<ICoordinate, bool>> lableddata, int parallelism = 5, double jitterscale = 1E-7, bool inorder = false, string debug=null)
        {
            producer = Task.Run(() =>
            {
                var pairs = new List<Tuple<ICoordinate, ICoordinate>>();
                var dict = lableddata.GroupBy(v => v.Item2).ToDictionary(v => v.Key, v => v.ToList());
                foreach(var negpt in dict[false])
                    foreach (var pospt in dict[true])
                        pairs.Add(new Tuple<ICoordinate, ICoordinate>(negpt.Item1, pospt.Item1));

                var problemDim = lableddata.First().Item1.GetDimensionality();
                var bisectors = pairs.AsParallel().Select(pair => problemDim == 2
                    ? (Hyperplane)Line.Bisector((Coordinate)pair.Item1, (Coordinate)pair.Item2, isCounted: false)
                    : (Hyperplane)Plane.Bisector((Coordinate3D)pair.Item1, (Coordinate3D)pair.Item2)).ToList();
                
                //add boundaries at +- 20 (assumes data is whitened)
                switch (problemDim)
                {
                    case 2:
                        bisectors.Add(new Line(0.001, -20, false));
                        bisectors.Add(new Line(-0.001, -20, false));
                        break;
                    case 3:
                        bisectors.Add(new Plane(jitterscale, 1+jitterscale, 1, 20));
                        bisectors.Add(new Plane(-jitterscale, 1-jitterscale, 1, 20));
                        bisectors.Add(new Plane(-jitterscale, 1 + jitterscale, 1, -20));
                        bisectors.Add(new Plane(jitterscale, 1 - jitterscale, 1, -20));
                        break;
                }
                if (debug != null)
                    File.WriteAllLines(debug, bisectors.Select(b => b.ToString()));
                //set inorder=true for enumeration without replacement of possible combinations.
                var problemSize = Special.Binomial(bisectors.Count, problemDim);
                if (inorder)
                {
                    //Lazily generate all (upto ~numsamples) permutations of bisectors pairs (2D) or triplets (3D)
                    var exhaustiveGroups = bisectors.DifferentCombinations(problemDim);
                    Parallel.ForEach(exhaustiveGroups, new ParallelOptions() { MaxDegreeOfParallelism = parallelism }, (inducerLst, loopState) =>
                    {
                        var inducers = inducerLst.ToList();
                        var jitteredPivots = GetPivotForCoordSet(inducers, jitterscale, inorder);
                        foreach(var piv in jitteredPivots)
                            Pivots.Add(piv);
                        if (Interlocked.Increment(ref NumPivots) >= numsamples)
                            loopState.Stop();
                    });
                }
                else
                {
                    //This is a coupon collector problem. E(x)=nlogn, and Pr[T>b*nlogn]<=n^(1-b)
                    Parallel.For(0, (long)(2.0*problemSize*Math.Log(problemSize)), new ParallelOptions() { MaxDegreeOfParallelism = parallelism }, (i, loopState) =>
                    {
                        var inducerIds = new HashSet<int>();
                        while (inducerIds.Count < problemDim)
                            inducerIds.Add(StaticConfigParams.rnd.Next(0, bisectors.Count));
                        var inducers = new List<Hyperplane>();
                        foreach (var id in inducerIds) inducers.Add(bisectors[id]);
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

        public static IEnumerable<ICoordinate> GetPivotForCoordSet(List<Hyperplane> inducers, double jitterscale = 1E-7, bool inorder=false)
        {
            var problemDim = inducers.Count;
            ICoordinate intersectionCoord, firstCoord, secondCoord;
            double firstX, secX;
            ICoordinate jitteredPivot = null;
            switch (problemDim)
            {
                case 2:
                    var firstbisectorLine = (Line) inducers[0];
                    var secondbisectorLine = (Line) inducers[1];
                    intersectionCoord = firstbisectorLine.Intersection(secondbisectorLine);
                    firstX = firstbisectorLine.EvaluateAtY(intersectionCoord.GetDimension(1) + jitterscale);
                    secX = secondbisectorLine.EvaluateAtY(intersectionCoord.GetDimension(1) + jitterscale);
                    jitteredPivot = new Coordinate((firstX + secX) / 2.0, intersectionCoord.GetDimension(1) + jitterscale);
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
                    try
                    {
                        intersectionCoord = new Coordinate3D(x[0, 0], x[1, 0], x[2, 0]);
                        firstX = firstbisectorPlane.EvaluateAtYZ(intersectionCoord.GetDimension(1) + jitterscale, intersectionCoord.GetDimension(2));
                        secX = secondbisectorPlane.EvaluateAtYZ(intersectionCoord.GetDimension(1) + jitterscale, intersectionCoord.GetDimension(2));
                        var thirdX = thirdbisectorPlane.EvaluateAtYZ(intersectionCoord.GetDimension(1) + jitterscale, intersectionCoord.GetDimension(2));
                        jitteredPivot = new Coordinate3D((firstX + secX + thirdX) / 3.0, intersectionCoord.GetDimension(1) + jitterscale, intersectionCoord.GetDimension(2));
                        
                    }
                    catch
                    {

                    }
                    yield return jitteredPivot;
                    /*
                    for (var i=0;i < Math.Pow(2,inorder ? 3 : 0); i++)
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
                    */
                    break;
            }
            
        }

        public IEnumerable<ICoordinate> GetPivots()
        {
            foreach (var el in Pivots.GetConsumingEnumerable())
                if (el != null)
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
