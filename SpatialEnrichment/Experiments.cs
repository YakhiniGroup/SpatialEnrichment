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
            StaticConfigParams.rnd = (Config.ActionList & Actions.Program_RandomConstSeed) != 0 ? new Random(1) : new Random();
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
                    var res = Program.RandomizeCoordinatesAndSave(numcoords, false);
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
    }
}
