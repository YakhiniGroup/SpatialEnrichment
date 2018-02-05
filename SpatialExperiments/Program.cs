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
            Directory.CreateDirectory("Experiments");

            var sizeid = -1;
            using (var graphfile = new StreamWriter($"Experiments\\aggregated_results.csv"))
                foreach (var N in sizes)
                {
                    Directory.CreateDirectory($"Experiments\\{N}");
                    sizeid++;
                    for (var i = 0; i < samples[sizeid]; i++)
                    {
                        var pos = (int)Math.Max(1, Math.Round((rnd.NextDouble() / 2.0) * N)); //less then half.
                        var neg = N - pos;
                        mHGJumper.Initialize(pos, neg);
                        var numCells = MathExtensions.NChooose3(mHGJumper.Lines + 1);
                        Console.WriteLine($"Analysis #{i} on size={N} with #cells={numCells}");
                        var dataset = SpatialEnrichmentWrapper.Helpers.SyntheticDatasets.SinglePlantedEnrichment(3);
                        File.WriteAllLines($"Experiments\\{N}\\data_{i}.csv", dataset.Select(c => c.Item1.ToString() + "," + c.Item2));

                        //var se = new EnrichmentWrapper(new ConfigParams() { ActionList = Actions.Search_CellSkipping });
                        //var res = (SpatialmHGResult3D)se.SpatialmHGWrapper3D(dataset.Select(v => new Tuple<double, double, double, bool>(v.Item1.GetDimension(0), v.Item1.GetDimension(1), v.Item1.GetDimension(2), v.Item2)).ToList()).First();
                        //var optres = new Tuple<ICoordinate, double, long>(new Coordinate3D(res.X, res.Y, res.Z), res.pvalue, res.mHGthreshold);
                        var optgrid = new Gridding();
                        optgrid.GenerateEmpricialDensityGrid((long)(100*numCells), dataset, inorder:true);
                        var optres = optgrid.EvaluateDataset(dataset, debug:"opt.csv");
                        
                        File.WriteAllText($"Experiments\\{N}\\data_{i}_optres.csv", $"{optres.Item1},{optres.Item2},{optres.Item3}");

                        var beadgrid = new Gridding();
                        beadgrid.GenerateBeadPivots(dataset);
                        var beadres = beadgrid.EvaluateDataset(dataset, debug: "bead.csv");

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
                            File.WriteAllText($"Experiments\\{N}\\data_{i}_optunifrom_{resolution}.csv", $"{unfiromres.Item1},{unfiromres.Item2},{unfiromres.Item3}");
                            graphfile.Write($",{unfiromres.Item2},{unfiromres.Item3}");
                        }
                        graphfile.WriteLine();
                        graphfile.Flush();
                    }
                }
        }

    }
}
