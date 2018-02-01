using SpatialEnrichment.Helpers;
using SpatialEnrichmentWrapper;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SpatialExperiments
{
    class Program
    {
        static void Main(string[] args)
        {

            Figure1();

        }

        /// <summary>
        /// figure with x axis = problem size, y axis = opt enrichment p-value. 
        /// Plot distribution on 100-1000 instances of types: 
        /// single planted enrichment, random, multiple planted enrichments. 
        /// For each instance, plot {flexible pivot, grid pivot}@10k,100k,1M , bead pivot.
        /// </summary>
        public static void Figure1()
        {
            var rnd = new Random();
            //arrayfun(@(x) nchoosek(x,3) , ([10, 20, 30, 50, 70]/2).^2) %<-- worst case sizes
            // 2300      161700     1873200    40495000   305627700 1.38e09
            var sizes = new int[] { 10, 20, 30, 50, 70, 90 };
            var samples = new int[] { 2000, 1000, 500, 100, 10, 10 }; //num experiments per size
            var griddepth = new int[] {1000, 10000, 100000};

            var sizeid = -1;
            using (var graphfile = new StreamWriter($"Experiments\\aggregated_results.csv"))
                foreach (var N in sizes)
                {
                    sizeid++;
                    for (var i = 0; i < samples[sizeid]; i++)
                    {
                        var pos = (int)Math.Round((rnd.NextDouble() / 2.0) * N); //less then half.
                        var neg = N - pos;
                        mHGJumper.Initialize(pos, neg);
                        var numCells = MathExtensions.NChooose3(mHGJumper.Lines + 1);
                        Console.WriteLine($"Analysis #{i} on size={N} with #cells={numCells}");
                        var dataset = SpatialEnrichmentWrapper.Helpers.SyntheticDatasets.SinglePlantedEnrichment(3);
                        File.WriteAllLines($"Experiments\\{N}\\data_{i}.csv", dataset.Select(c => c.Item1.ToString() + "," + c.Item2));
                        var optgrid = new Gridding();
                        optgrid.GenerateEmpricialDensityGrid((long)numCells, dataset);
                        var optres = optgrid.EvaluateDataset(dataset);
                        File.WriteAllText($"Experiments\\{N}\\data_{i}_optres.csv", $"{optres.Item1},{optres.Item2},{optres.Item3}");

                        var beadgrid = new Gridding();
                        beadgrid.GenerateBeadPivots(dataset);
                        var beadres = beadgrid.EvaluateDataset(dataset);

                        graphfile.Write($"{N},{i},{numCells},{optres.Item2},{optres.Item3},{beadres.Item2},{beadres.Item3}");

                        foreach (var resolution in griddepth)
                        {
                            var empiricalgrid = new Gridding();
                            empiricalgrid.GenerateEmpricialDensityGrid(resolution, dataset);
                            var empiricalres = empiricalgrid.EvaluateDataset(dataset);
                            File.WriteAllText($"Experiments\\{N}\\data_{i}_optempirical_{resolution}.csv", $"{empiricalres.Item1},{empiricalres.Item2},{empiricalres.Item3}");
                            graphfile.Write($",{empiricalres.Item2},{empiricalres.Item3}");
                            var uniformgrid = new Gridding();
                            uniformgrid.GeneratePivotGrid(resolution, 3);
                            var unfiromres = uniformgrid.EvaluateDataset(dataset);
                            File.WriteAllText($"Experiments\\{N}\\data_{i}_optempirical_{resolution}.csv", $"{unfiromres.Item1},{unfiromres.Item2},{unfiromres.Item3}");
                            graphfile.Write($",{unfiromres.Item2},{unfiromres.Item3}");
                        }
                        graphfile.WriteLine();
                        graphfile.Flush();
                    }
                }
        }

    }
}
