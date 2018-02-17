using SpatialEnrichment.Helpers;
using SpatialEnrichmentWrapper;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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
            //Debug2DSamplingVsGrid();
            try
            {
                CompareTimeVsQualityOnRealData(args[0], args.Length > 1 ? int.Parse(args[1]) : 500000);
            }
            catch (Exception ex)
            {
                Console.WriteLine(@"Failed processing file: {0}", args[0]);
            }
            //Figure1();

        }


        public static void CompareTimeVsQualityOnRealData(string file, int pivots)
        {
            var filename = Path.GetFileNameWithoutExtension(file);
            //var file = @"C:\Users\shaybe\Dropbox\Thesis-PHd\SpatialEnrichment\bSubtilis\Prepped\fatty_acid_biosynthetic_process.csv";
            var data = File.ReadAllLines(file).Select(l => l.Split(',')).Select(sl =>
                    new Tuple<ICoordinate, bool>(new Coordinate3D(double.Parse(sl[0]), double.Parse(sl[1]), double.Parse(sl[2])), sl[3] == "1")).ToList();

            var nrm = new Normalizer(data.Select(d => d.Item1).ToList());
            var normalizedData = nrm.Normalize(data.Select(d => d.Item1).ToList());
            var normalizedDataset = normalizedData.Zip(data, (a, b) => new Tuple<ICoordinate, bool>(a, b.Item2)).ToList();
            mHGJumper.Initialize(data.Count(v => v.Item2), data.Count(v => !v.Item2));
            mHGJumper.optHGT = 1;

            Console.WriteLine($"{DateTime.Now}: Sarting Sampling.");
            var sampling = new Gridding();
            sampling.StartTimeDebug($"TimeQuality_sampling_{filename}.csv", nrm, 500);
            sampling.GenerateEmpricialDensityGrid(pivots, normalizedDataset);
            sampling.EvaluateDataset(normalizedDataset);
            sampling.StopTimeDebug();
            Console.WriteLine($"{DateTime.Now}: Done with Sampling. Starting Grid.");
            var gridding = new Gridding();
            gridding.StartTimeDebug($"TimeQuality_grid_{filename}.csv", nrm, 500);
            gridding.GeneratePivotGrid(pivots, 3);
            gridding.EvaluateDataset(normalizedDataset);
            gridding.StopTimeDebug();
            Console.WriteLine($"{DateTime.Now}: Done with Gridding.");
        }

        public static void Debug2DSamplingVsGrid()
        {
            Directory.CreateDirectory($"Eval");
            var rnd = new Random(0);
            int wins = 0;
            int losses = 0;
            for(var i=0;i<100;i++)
            {
                var N = 100;
                var pos = (int)Math.Max(1, Math.Round((rnd.NextDouble() / 2.0) * N)); //less then half.
                var neg = N - pos;
                mHGJumper.Initialize(pos, neg);
                var numCells = (long)MathExtensions.NChooose2(mHGJumper.Lines + 1);
                var evalcount = numCells; //1000
                var dataset = SpatialEnrichmentWrapper.Helpers.SyntheticDatasets.SinglePlantedEnrichment(2);
                File.WriteAllLines($"Eval\\dataset.csv", dataset.Select(c => c.Item1.ToString() + "," + c.Item2));

                var samplegrid = new Gridding();
                string bisectorDebug = null; //$"Eval\\bisectors.csv";
                string samplingDebug = null; //$"Eval\\sampling.csv"
                bool isOrdered = false; //true
                samplegrid.GenerateEmpricialDensityGrid(evalcount, dataset, inorder: isOrdered, debug: bisectorDebug);
                var sampleres = samplegrid.EvaluateDataset(dataset, debug: samplingDebug);

                var pivotgrid = new Gridding();
                string gridDebug = null; //$"Eval\\grid.csv"
                pivotgrid.GeneratePivotGrid(evalcount);
                var pivotres = pivotgrid.EvaluateDataset(dataset, debug: gridDebug);
                File.WriteAllText(@"Eval\\best.csv", sampleres.Item1.ToString() + "\n" + pivotres.Item1.ToString());
                Console.WriteLine($"#{i}:{wins}/{losses} : {sampleres.Item2}, {pivotres.Item2}");
                if (sampleres.Item2 < pivotres.Item2)
                    wins++;
                if (sampleres.Item2 > pivotres.Item2)
                    losses++;
            }
        }


        /// <summary>
        /// figure with x axis = problem size, y axis = opt enrichment p-value. 
        /// Plot distribution on 100-1000 instances of types: 
        /// single planted enrichment, random, multiple planted enrichments. 
        /// For each instance, plot {flexible pivot, grid pivot}@10k,100k,1M , bead pivot.
        /// </summary>
        public static void Figure1()
        {
            var rnd = new Random(0);
            //arrayfun(@(x) nchoosek(x,3) , ([10, 20, 30, 50, 70]/2).^2) %<-- worst case sizes
            // 2300      161700     1873200    40495000   305627700 1.38e09
            var sizes = new int[] { 10, 20, 30, 50, 70, 90 };
            var samples = new int[] { 500, 400, 400, 100, 10, 10 }; //num experiments per size
            var griddepth = new int[] { 100, 1000, 10000, 20000, 50000, 100000 };
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
                        optgrid.GenerateEmpricialDensityGrid((long)(100*numCells), dataset, inorder:true, debug: $"Experiments\\{N}\\Planes.csv");
                        var optres = optgrid.EvaluateDataset(dataset);
                        File.WriteAllText($"Experiments\\{N}\\data_{i}_optres.csv", $"{optres.Item1},{optres.Item2},{optres.Item3}");

                        var beadgrid = new Gridding();
                        beadgrid.GenerateBeadPivots(dataset);
                        var beadres = beadgrid.EvaluateDataset(dataset);
                        File.WriteAllText($"Experiments\\{N}\\data_{i}_beadres.csv", $"{beadres.Item1},{beadres.Item2},{beadres.Item3}");
                        graphfile.Write($"{N},{i},{numCells},{optres.Item2},{optres.Item3},{beadres.Item2},{beadres.Item3}");

                        foreach (var resolution in griddepth)
                        {
                            var empiricalgrid = new Gridding();
                            empiricalgrid.GenerateEmpricialDensityGrid(resolution, dataset, inorder:false);
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
