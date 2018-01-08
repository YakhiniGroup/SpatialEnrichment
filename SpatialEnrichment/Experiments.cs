using SpatialEnrichmentWrapper;
using SpatialEnrichment.Helpers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SpatialEnrichment
{
    public static class Experiments
    {
        static ConfigParams Config;



        public static void CompareExhaustiveWithPivots(int numcoords = 50, int numiter=500)
        {
            Config = new ConfigParams("");
            #region init
            StaticConfigParams.rnd = (Config.ActionList & Actions.Program_RandomConstSeed) != 0 ? new SafeRandom(1) : new SafeRandom();
            Config.timer.Start();
            #endregion
            //Load coordinates and labels
            var identities = new List<string>();
            var resultPairedDiff = new List<double>();
            int victories = 0, ties = 0;
            using (var fileout = new StreamWriter(@"pivot_vs_exhaustive.csv"))
                for (var instanceIter = 0; instanceIter < numiter; instanceIter++)
                {
                    var coordinates = new List<ICoordinate>();
                    var labels = new List<bool>();
                    StaticConfigParams.filenamesuffix = instanceIter.ToString();
                    Console.WriteLine("File {0}", instanceIter);
                    var res = Program.RandomizeCoordinatesAndSave(numcoords, out var planted, false);
                    coordinates = res.Item1;
                    labels = res.Item2;
                    var zeros = labels.Count(l => l == false);
                    var filterCount = (int)(Config.FilterKFurthestZeros * zeros);
                    if (filterCount > 0)
                    {
                        Console.WriteLine("Filtering {0} far away points", filterCount);
                        var positives = new List<ICoordinate>();
                        var negatives = new List<ICoordinate>();
                        var negIds = new List<int>();
                        for (var i = 0; i < coordinates.Count; i++)
                        {
                            if (labels[i])
                                positives.Add(coordinates[i]);
                            else
                            {
                                negatives.Add(coordinates[i]);
                                negIds.Add(i);
                            }
                        }
                        var negMinDist = new HashSet<int>(negatives.Zip(negIds, (a, b) => new { PosMinDist = positives.Select(p => p.EuclideanDistance(a)).Min(), Id = b })
                            .OrderByDescending(n => n.PosMinDist).Select(t => t.Id).Take(filterCount));
                        coordinates = coordinates.Where((a, b) => !negMinDist.Contains(b)).ToList();
                        labels = labels.Where((a, b) => !negMinDist.Contains(b)).ToList();
                        numcoords -= filterCount;
                    }

                    //Actual work starts here
                    var ones = labels.Count(l => l);
                    var linecount = ones * (numcoords - ones);
                    Config.Cellcount = ((long)linecount * (linecount - 1)) / 2.0 + linecount + 1;

                    mHGJumper.Initialize(ones, numcoords - ones);
                    mHGJumper.optHGT = Config.SIGNIFICANCE_THRESHOLD;// / Cellcount; //for bonferonni
                                                                     //alpha is the Bonferonni (union-bound) corrected significance level
                    Tesselation T = null;
                    var ew = new EnrichmentWrapper(Config);
                    Console.WriteLine(@"Starting work on {0} coordinates with {1} 1's (|cells|={2:n0}, alpha={3}).", numcoords, ones, Config.Cellcount, mHGJumper.optHGT);
                    var instanceData = coordinates.Zip(labels, (a, b) => new Tuple<double, double, bool>(a.GetDimension(0), a.GetDimension(1), b)).ToList();
                    var resultsExhaustive = ew.SpatialmHGWrapper(instanceData).Select(v => (SpatialmHGResult)v).First();
                    var resultsPivot = ew.mHGPivotWrapper(instanceData).Select(v => (SpatialmHGResult)v).First();
                    fileout.WriteLine($"{resultsExhaustive.pvalue}, {resultsPivot.pvalue}");
                    
                    if (resultsExhaustive.pvalue < resultsPivot.pvalue)
                        victories++;
                    else if (resultsExhaustive.pvalue == resultsPivot.pvalue)
                        ties++;
                    else
                        Console.WriteLine($"Debug me");
                    resultPairedDiff.Add(Math.Log10(resultsPivot.pvalue) - Math.Log10(resultsExhaustive.pvalue));
                }

            Console.WriteLine($"Out of {numiter} iterations, spatial enrichment won in {victories} and tied in {ties}.");
            Console.WriteLine("Total elapsed time: {0:g}.\nPress any key to continue.", Config.timer.Elapsed);
            File.WriteAllLines("experiment_pvaldiffs.txt", resultPairedDiff.Select(v=>v.ToString()).ToArray());
            Console.ReadKey();
        }


        /// <summary>
        /// 3d subsample from 50 points 20. run 100 times. compare to opt.
        /// </summary>
        /// <param name="numcoords"></param>a
        /// <param name="numiter"></param>
        public static void CompareExahustiveWithSubsamplingInput(int numcoords = 50, int subsampleSize = 20, int numiter = 100, string suffix="0")
        {
            Config = new ConfigParams("");
            Program.Config = Config;
            #region init
            StaticConfigParams.rnd = (Config.ActionList & Actions.Program_RandomConstSeed) != 0 ? new SafeRandom(1) : new SafeRandom();
            Config.timer.Start();
            #endregion
            //Load coordinates and labels

            using (var fileout = new StreamWriter($"sample_vs_exhaustive_{suffix}.csv"))
                for (var instanceIter = 1; instanceIter < numiter; instanceIter++)
                {
                    if (instanceIter == 1)
                        fileout.WriteLine(
                            @"PlantedX,PlantedY,ExhaustiveX,ExhaustiveY,ExhaustivePval,SubsampleX,SubsampleY,"+
                            @"SubsamplePval,UniformX,UniformY,UniformPval,EmpiricalX,EmpiricalY,EmpiricalPval");
                    #region init instance
                    Console.WriteLine("Iteration {0}", instanceIter);
                    StaticConfigParams.filenamesuffix = $"{suffix}_{instanceIter}";
                    var res = Program.RandomizeCoordinatesAndSave(numcoords, out var planted, true);
                    
                    var coordinates = res.Item1;
                    var labels = res.Item2;
                    var instanceDataCoords = coordinates.Zip(labels, (a, b) => new Tuple<ICoordinate, bool>(a, b)).ToList();
                    var plantedPval = mHGJumper.minimumHypergeometric(instanceDataCoords.OrderBy(c => c.Item1.EuclideanDistance(planted))
                        .Select(v => v.Item2).ToArray()).Item1;
                    var instanceData = coordinates.Zip(labels, (a, b) => new Tuple<double, double, bool>(a.GetDimension(0), a.GetDimension(1), b)).ToList();
                    Config.SKIP_SLACK = -1000000; //exhaustive, no skips
                    #endregion

                    #region exhaustive
                    Config.ActionList = Actions.Search_CellSkipping | Actions.Instance_PlantedSingleEnrichment;
                    var ew = new EnrichmentWrapper(Config);
                    var resultsExhaustive = ew.SpatialmHGWrapper(instanceData).Select(v => (SpatialmHGResult)v).First();
                    var ones = labels.Count(l => l);
                    var linecount = ones * (numcoords - ones);
                    Config.Cellcount = ((long)linecount * (linecount - 1)) / 2.0 + linecount + 1;

                    mHGJumper.Initialize(ones, numcoords - ones);
                    mHGJumper.optHGT = Config.SIGNIFICANCE_THRESHOLD;
                    Line.Reset();
                    #endregion

                    #region subsample coords
                    var sampleCoords = coordinates
                        .Zip(labels, (a, b) => new {Coords = a, Labels = b, Rand = StaticConfigParams.rnd.Next()})
                        .OrderBy(v => v.Rand).Take(subsampleSize).ToList();
                    while(sampleCoords.All(v=>v.Labels) || !sampleCoords.Any(v => v.Labels))
                        sampleCoords = coordinates
                            .Zip(labels, (a, b) => new { Coords = a, Labels = b, Rand = StaticConfigParams.rnd.Next() })
                            .OrderBy(v => v.Rand).Take(subsampleSize).ToList();
                    Generics.SaveToCSV(sampleCoords.Select(t=> t.Coords.ToString() + "," + Convert.ToDouble(t.Labels)),
                        $@"coords_{StaticConfigParams.filenamesuffix}_subpop.csv");

                    ones = sampleCoords.Count(l => l.Labels);
                    linecount = ones * (subsampleSize - ones);
                    Config.Cellcount = ((long)linecount * (linecount - 1)) / 2.0 + linecount + 1;
                    
                    Console.WriteLine(@"Starting work on {0} coordinates with {1} 1's (|cells|={2:n0}, alpha={3}).", numcoords, ones, Config.Cellcount, mHGJumper.optHGT);
                    mHGJumper.optHGT = Config.SIGNIFICANCE_THRESHOLD;
                    Tesselation T = new Tesselation(sampleCoords.Select(v => (Coordinate)v.Coords).ToList(), sampleCoords.Select(v => v.Labels).ToList(), null, Config)
                    {
                        ProjectedFrom = coordinates,
                        SourceLabels = labels.ToArray()
                    };
                    var topResults = T.GradientSkippingSweep(numStartCoords: 20, numThreads: Environment.ProcessorCount - 1).First();
                    Line.Reset();
                    #endregion
                    
                    #region sampling strategies
                    mHGJumper.optHGT = Config.SIGNIFICANCE_THRESHOLD;
                    Console.Write($"Uniform grid strategy @{Config.Cellcount} pivots... ");
                    var uniformGridFactory = new Gridding();
                    uniformGridFactory.GeneratePivotGrid(Convert.ToInt64(Config.Cellcount));
                    var uniformGridPivotlst = uniformGridFactory.GetPivots().ToList();
                    var uniformGridPivot = uniformGridPivotlst.AsParallel()
                        .Select(p => new { Pivot = p, Enrichment = -Math.Log10(EnrichmentAtPivot(instanceDataCoords, p)) })
                        .MaxBy(p=>p.Enrichment);
                    Console.WriteLine($"p={uniformGridPivot:e}");
                    Console.Write($"Empirical grid strategy @{Config.Cellcount} pivots... ");
                    mHGJumper.optHGT = Config.SIGNIFICANCE_THRESHOLD;
                    var empiricalGridFactory = new Gridding();
                    empiricalGridFactory.GenerateEmpricialDensityGrid(Convert.ToInt64(Config.Cellcount), instanceDataCoords);
                    var empiricalGridPivotlst = empiricalGridFactory.GetPivots().ToList();
                    var empiricalGridPivot = empiricalGridPivotlst.AsParallel()
                        .Select(p => new {Pivot = p, Enrichment = -Math.Log10(EnrichmentAtPivot(instanceDataCoords, p))})
                        .OrderByDescending(p => p.Enrichment).First();
                    Console.WriteLine($"p={empiricalGridPivot:e}");
                    #endregion

                    fileout.WriteLine(
                        $"{planted.ToString()},{-Math.Log10(plantedPval)}," +
                        $"{resultsExhaustive.X},{resultsExhaustive.Y},{-Math.Log10(resultsExhaustive.pvalue)}, " +
                        $"{topResults.CenterOfMass},{-Math.Log10(topResults.mHG.Item1)}," +
                        $"{uniformGridPivot.Pivot},{uniformGridPivot.Enrichment}," +
                        $"{empiricalGridPivot.Pivot},{empiricalGridPivot.Enrichment}");
                    fileout.Flush();
                }
            Console.WriteLine("Total elapsed time: {0:g}.\nPress any key to continue.", Config.timer.Elapsed);
        }
        public static double EnrichmentAtPivot(List<Tuple<ICoordinate, bool>> data, ICoordinate pivot)
        {
            var binvec = data.OrderBy(c => c.Item1.EuclideanDistance(pivot)).Select(c => c.Item2).ToArray();
            var res = mHGJumper.minimumHypergeometric(binvec);
            return res.Item1;
        }
    }
}
