using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CommandLine;
using SpatialEnrichment.Helpers;
using SpatialEnrichmentWrapper;

namespace SpatialEnrichment
{
    public class Program
    {
        public static ConfigParams Config;
        static void Main(string[] args)
        {
            var options = new CommandlineParameters();
            var isValid = Parser.Default.ParseArgumentsStrict(args, options);
            
            //args = new[] {@"c:\Users\shaybe\Dropbox\Thesis-PHd\SpatialEnrichment\Datasets\usStatesBordersData.csv"};
            //args = new[] { @"c:\Users\shaybe\Dropbox\Thesis-PHd\SpatialEnrichment\Caulobacter\transferases\acetyltransferase.csv" };
            var numcoords = 300;
            Config = new ConfigParams("");

            if((Config.ActionList & Actions.Experiment_ComparePivots) != 0)
            {
                Console.WriteLine(@"Running pivot comparison experiment");
                Experiments.CompareExhaustiveWithPivots(numcoords, numiter:30);
                return;
            }
            if ((Config.ActionList & Actions.Experiment_SampleLines) != 0)
            {
                Console.WriteLine(@"Running sampling comparison experiment");
                var subsamples = new[] {10, 20, 30};
                var population = new[] {40, 60, 100};
                var counter = 0;
                foreach(var nu in subsamples)
                foreach (var N in population)
                {
                    Experiments.CompareExahustiveWithSubsamplingInput(N, nu, 50, counter++.ToString());
                }
                return;
            }

            if(Config.SKIP_SLACK != 0)
                Console.WriteLine(@"Warning! Current configuration uses CONST_SKIP_SLACK={0}", Config.SKIP_SLACK);
            if (StaticConfigParams.WriteToCSV)
                Console.WriteLine(@"Warning! Current configuration writes cells to CSV - this is SLOW.");

            #region init
            StaticConfigParams.rnd = (Config.ActionList & Actions.Program_RandomConstSeed) != 0 ? new SafeRandom(1) : new SafeRandom();
            Config.timer.Start();
            #endregion

            foreach (var dir in new List<string>() { "Cells", "Planes" })
            {
                var di = new DirectoryInfo(dir);
                if (!di.Exists)
                    di.Create();
                foreach (FileInfo file in di.GetFiles())
                {
                    file.Delete();
                }
            }
            foreach (var filemask in new List<string>() { "lines_*.csv", "coordSample_*.csv " })
            {
                FileInfo[] taskFiles = new DirectoryInfo(Directory.GetCurrentDirectory()).GetFiles(filemask); 
                foreach (FileInfo file in taskFiles)
                    file.Delete();
            }
            //Load coordinates and labels
            var infile = Path.GetFileNameWithoutExtension(args.Length>0?args[0]:"");
            var identities = new List<string>();
            for (var instanceIter = 0; instanceIter < 1; instanceIter++)
            {
                var coordinates = new List<ICoordinate>();
                List<bool> labels = null;
                StaticConfigParams.filenamesuffix = instanceIter.ToString();
                Console.WriteLine("File {0}",instanceIter);
                if (args.Length > 0)
                    if (File.Exists(args[0]))
                    {
                        var res = LoadCoordinatesFromFile(args, ref numcoords, identities);
                        coordinates = res.Item1;
                        labels = res.Item2;
                    }
                    else
                        throw new ArgumentException("Input file not found!");
                else
                {
                    var res = RandomizeCoordinatesAndSave(numcoords);
                    coordinates = res.Item1;
                    labels = res.Item2;
                }

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

                //Look at lazy caretaker numbers. Note, we don't actually cover open cells 
                //so its -linecount as each line has two open cells on either side of it, 
                //and each open cell is made up of two lines.

                mHGJumper.Initialize(ones, numcoords - ones);
                mHGJumper.optHGT = Config.SIGNIFICANCE_THRESHOLD;// / Cellcount; //for bonferonni
                //alpha is the Bonferonni (union-bound) corrected significance level
                
                //Debugging.debug_mHG(numcoords,ones);
                Tesselation T = null;
                var coordType = coordinates.First().GetType();
                var ew = new EnrichmentWrapper(Config);
                List<ISpatialmHGResult> results = null;
                if (coordType == typeof(Coordinate3D))
                {
                    Config.Cellcount += MathExtensions.Binomial(linecount, 3);
                    Console.WriteLine(@"Projecting 3D problem to collection of 2D {0} coordinates with {1} 1's (|cells|={2:n0}, alpha={3}).", numcoords, ones, Config.Cellcount, mHGJumper.optHGT);
                    results = ew.SpatialmHGWrapper3D(coordinates.Zip(labels,
                        (a, b) => new Tuple<double, double, double, bool>(a.GetDimension(0), a.GetDimension(1),
                            a.GetDimension(2), b)).ToList(), options.BatchMode);
                }
                else if (coordType == typeof(Coordinate))
                {
                    Console.WriteLine(@"Starting work on {0} coordinates with {1} 1's (|cells|={2:n0}, alpha={3}).", numcoords, ones, Config.Cellcount, mHGJumper.optHGT);
                    results = ew.SpatialmHGWrapper(coordinates.Zip(labels, (a, b) => 
                        new Tuple<double, double, bool>(a.GetDimension(0), a.GetDimension(1), b)).ToList());
                }

                for (var resid = 0; resid < results.Count; resid++)
                {
                    results[resid].SaveToCSV($@"Cells\{infile}_Cell_{resid}_{StaticConfigParams.filenamesuffix}.csv");
                }
                if (Config.mHGlist.Any())
                    using (var outfile = new StreamWriter($"{infile}_mhglist_{StaticConfigParams.filenamesuffix}.csv"))
                        foreach (var res in Config.mHGlist.Where(t => t != null))
                            outfile.WriteLine("{0},{1}", res.Item2, res.Item1);
                if (options.BatchMode)
                {
                    AzureBatchExecution.UploadFileToContainer($"{infile}_mhglist_{StaticConfigParams.filenamesuffix}.csv", options.SaasUrl);
                    for (var resid = 0; resid < results.Count; resid++)
                        AzureBatchExecution.UploadFileToContainer($@"Cells\{infile}_Cell_{resid}_{StaticConfigParams.filenamesuffix}.csv", options.SaasUrl);
                }
                File.WriteAllLines(Path.ChangeExtension(infile, "out"), results.Select(r=>r.ToString()));
            }

            //Finalize
            if (args.Length == 0 || Debugger.IsAttached)
            {
                Console.WriteLine("Total elapsed time: {0:g}.\nPress any key to continue.", Config.timer.Elapsed);
                Console.ReadKey();
            }
        }

        public static void mHGOnOriginalPoints(string[] args, List<ICoordinate> coordinates, List<bool> labels, int numcoords, List<ICoordinate> pivots = null)
        {
            Console.WriteLine(@"Covering original points with mHG.");
            int ptcount = 0;
            var rescol = new BlockingCollection<string>();
            var wrtr = Task.Run(() =>
            {
                var outfile = Path.GetFileNameWithoutExtension(args[0]) + "_output.csv";
                using (var file = new StreamWriter(outfile))
                    foreach (var l in rescol.GetConsumingEnumerable())
                        file.WriteLine(l);
            });
            var labeledCoords = coordinates.Zip(labels, (a, b) => new {Coord = a, Label = b}).ToList();
            var Pivots = pivots ?? coordinates;
            Parallel.ForEach(Pivots, coord =>
            {
                var ordDat = labeledCoords.OrderBy(c => coord.EuclideanDistance(c.Coord)).ToList();
                var vec = ordDat.Select(c => c.Label).ToArray();
                var res = mHGJumper.minimumHypergeometric(vec);
                if (Pivots.Count < 10 || res.Item1 < 0.05)
                    rescol.Add(coord.ToString() + "," + res.Item1 + "," +
                               ordDat[res.Item2].Coord.EuclideanDistance(coord));
                Console.Write("\r\r\r\rCovered {0}/{1}.", Interlocked.Increment(ref ptcount), numcoords);
            });
            rescol.CompleteAdding();
            wrtr.Wait();
        }

        public static Tuple<List<ICoordinate>, List<bool>> RandomizeCoordinatesAndSave(int numcoords, bool save=true)
        {
            List<ICoordinate> coordinates = new List<ICoordinate>();
            List<bool> labels = new List<bool>();
            bool instance_created = false;
            while(!instance_created)
            { 
                if ((Config.ActionList & Actions.Instance_Uniform) != 0)
                {
                    for (var i = 0; i < numcoords; i++)
                    {
                        if(StaticConfigParams.RandomInstanceType == typeof(Coordinate))
                            coordinates.Add(Coordinate.MakeRandom());
                        else if (StaticConfigParams.RandomInstanceType == typeof(Coordinate3D))
                            coordinates.Add(Coordinate3D.MakeRandom());
                        labels.Add(StaticConfigParams.rnd.NextDouble() > StaticConfigParams.CONST_NEGATIVELABELRATE);
                    }        
                }
                if ((Config.ActionList & Actions.Instance_PlantedSingleEnrichment) != 0)
                {
                    for (var i = 0; i < numcoords; i++)
                        if (StaticConfigParams.RandomInstanceType == typeof(Coordinate))
                            coordinates.Add(Coordinate.MakeRandom());
                        else if (StaticConfigParams.RandomInstanceType == typeof(Coordinate3D))
                            coordinates.Add(Coordinate3D.MakeRandom());
                    ICoordinate pivotCoord = null;
                    if (StaticConfigParams.RandomInstanceType == typeof(Coordinate))
                        pivotCoord = Coordinate.MakeRandom();
                    else if (StaticConfigParams.RandomInstanceType == typeof(Coordinate3D))
                        pivotCoord = Coordinate3D.MakeRandom();

                    var prPos = (int) Math.Round((1.0 - StaticConfigParams.CONST_NEGATIVELABELRATE) * numcoords);
                    mHGJumper.Initialize(prPos, numcoords - prPos);
                    coordinates = coordinates.OrderBy(t => t.EuclideanDistance(pivotCoord)).ToList();
                    labels = mHGJumper.SampleSignificantEnrichmentVector(1e-3).ToList();
                    Console.WriteLine($"Instantiated sample with p={mHGJumper.minimumHypergeometric(labels.ToArray()).Item1:e} around pivot {pivotCoord.ToString()}");
                    mHGJumper.optHGT = 0.05;
                }
                instance_created = labels.Any();
            }
            if (save)
                Generics.SaveToCSV(coordinates.Zip(labels, (a, b) => a.ToString() +","+ Convert.ToDouble(b)),
                    $@"coords_{StaticConfigParams.filenamesuffix}.csv",true);
            return new Tuple<List<ICoordinate>, List<bool>>(coordinates, labels);
        }

        
        private static Tuple<List<ICoordinate>, List<bool>> LoadCoordinatesFromFile(string[] args, ref int numcoords, List<string> identities)
        {
            var lines = File.ReadLines(args[0]).Select(l => l.Split(','));
            numcoords = 0;
            List<ICoordinate> coordinates = new List<ICoordinate>();
            List<bool> labels = new List<bool>();

            foreach (var line in lines)
            {
                numcoords ++;
                if (line.Length == 4) 
                {
                    double v;
                    if (line.Take(3).All(l => double.TryParse(l, out v))) //has 3d coords
                    {
                        coordinates.Add(new Coordinate3D(Convert.ToDouble(line[0]), Convert.ToDouble(line[1]), Convert.ToDouble(line[2])));
                        labels.Add((line[3] == "1" || line[3].ToLowerInvariant() == "true"));
                    }
                    else //has identities
                    {
                        identities.Add(line[0]);
                        coordinates.Add(new Coordinate(Convert.ToDouble(line[1]), Convert.ToDouble(line[2])));
                        labels.Add((line[3] == "1" || line[3].ToLowerInvariant() == "true"));
                    }
                }
                else if (line.Length == 3)
                {
                    coordinates.Add(new Coordinate(Convert.ToDouble(line[0]), Convert.ToDouble(line[1])));
                    labels.Add((line[2] == "1" || line[2].ToLowerInvariant() == "true"));
                }
                else if (line.Length == 2)
                {
                    coordinates.Add(new Coordinate(Convert.ToDouble(line[0]), Convert.ToDouble(line[1])));
                    labels.Add(StaticConfigParams.rnd.NextDouble() > 0.5);
                }
            }
            return new Tuple<List<ICoordinate>, List<bool>>(coordinates, labels);
        }

        


    }
}
