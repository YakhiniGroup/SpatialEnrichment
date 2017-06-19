﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SpatialEnrichment.Helpers;
using SpatialEnrichmentWrapper;

namespace SpatialEnrichment
{
    class Program
    {
        static void Main(string[] args)
        {
            //args = new[] {@"c:\Users\shaybe\Dropbox\Thesis-PHd\SpatialEnrichment\Datasets\usStatesBordersData.csv"};
            //args = new[] { @"c:\Users\shaybe\Dropbox\Thesis-PHd\SpatialEnrichment\Caulobacter\transferases\acetyltransferase.csv" };
            var numcoords = 20;
            if(StaticConfigParams.CONST_SKIP_SLACK != 0)
                Console.WriteLine(@"Warning! Current configuration uses CONST_SKIP_SLACK={0}", StaticConfigParams.CONST_SKIP_SLACK);
            if (StaticConfigParams.WriteToCSV)
                Console.WriteLine(@"Warning! Current configuration writes cells to CSV - this is SLOW.");

            #region init
            StaticConfigParams.rnd = (StaticConfigParams.ActionList & Actions.Program_RandomConstSeed) != 0 ? new Random(1) : new Random();
            StaticConfigParams.timer.Start();
            #endregion

            if (StaticConfigParams.WriteToCSV)
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
            //Load coordinates and labels
            var identities = new List<string>();

            for (var instanceIter = 0; instanceIter < 1; instanceIter++)
            {
                var coordinates = new List<ICoordinate>();
                var labels = new List<bool>();
                StaticConfigParams.filenamesuffix = instanceIter.ToString();
                Console.WriteLine("File {0}",instanceIter);
                if (args.Length>0) 
                    if(File.Exists(args[0]))
                        LoadCoordinatesFromFile(args, ref numcoords, coordinates, labels, identities, StaticConfigParams.rnd);
                    else
                        throw new ArgumentException("Input file not found!");
                else
                    RandomizeCoordinatesAndSave(numcoords, coordinates, StaticConfigParams.rnd, labels);
                //Actual work starts here
                var ones = labels.Count(l => l);
                var linecount = ones * (numcoords - ones);
                StaticConfigParams.Cellcount = ((long)linecount * (linecount - 1)) / 2.0 + linecount + 1;
                //Look at lazy caretaker numbers. Note, we don't actually cover open cells 
                //so its -linecount as each line has two open cells on either side of it, 
                //and each open cell is made up of two lines.

                mHGJumper.Initialize(ones, numcoords - ones);
                mHGJumper.optHGT = StaticConfigParams.CONST_SIGNIFICANCE_THRESHOLD;// / Cellcount; //for bonferonni
                //alpha is the Bonferonni (union-bound) corrected significance level
                
                //Debugging.debug_mHG(numcoords,ones);
                Tesselation T = null;
                var coordType = coordinates.First().GetType();
                if (coordType == typeof(Coordinate3D))
                {
                    var ew = new EnrichmentWrapper();
                    Console.WriteLine(@"Projecting 3D problem to collection of 2D {0} coordinates with {1} 1's (|cells|={2:n0}, alpha={3}).", numcoords, ones, StaticConfigParams.Cellcount, mHGJumper.optHGT);
                    ew.SpatialmHGWrapper3D(coordinates.Zip(labels, 
                        (a, b) => new Tuple<double, double, double, bool>(a.GetDimension(0), a.GetDimension(1), a.GetDimension(2),b)).ToList());

                }
                else if (coordType == typeof(Coordinate))
                {
                    Console.WriteLine(@"Starting work on {0} coordinates with {1} 1's (|cells|={2:n0}, alpha={3}).", numcoords, ones, StaticConfigParams.Cellcount, mHGJumper.optHGT);
                    T = new Tesselation(coordinates.Select(c => (Coordinate) c).ToList(), labels, identities);
                
                    if ((StaticConfigParams.ActionList & Actions.Search_CoordinateSample) != 0)
                    {
                        T.GradientSkippingSweep(
                        numStartCoords: 20,
                        numThreads: Environment.ProcessorCount - 1);
                        //numStartCoords: 1,
                        //numThreads: 1);
                    }
                    if ((StaticConfigParams.ActionList & Actions.Search_Exhaustive) != 0)
                    {
                        T.GenerateFromCoordinates();
                    }
                    if ((StaticConfigParams.ActionList & Actions.Search_Originals) != 0)
                    {
                        mHGOnOriginalPoints(args, coordinates, labels, numcoords);
                    }
                    if ((StaticConfigParams.ActionList & Actions.Search_FixedSet) != 0)
                    {
                        var avgX = coordinates.Select(c => c.GetDimension(0)).Average();
                        var avgY = coordinates.Select(c => c.GetDimension(1)).Average();
                        var cord = new Coordinate(avgX, avgY);
                        mHGOnOriginalPoints(args, coordinates, labels, numcoords, new List<ICoordinate>() { cord });
                    }
                    if ((StaticConfigParams.ActionList & Actions.Search_LineSweep) != 0)
                    {
                        T.LineSweep();
                    }
                    Tesselation.Reset();
                }
            }
            
            //Finalize
            if(args.Length == 0 || Debugger.IsAttached)
            {
                Console.WriteLine("Total elapsed time: {0:g}.\nPress any key to continue.", StaticConfigParams.timer.Elapsed);
                Console.ReadKey();
            }
        }

        private static void mHGOnOriginalPoints(string[] args, List<ICoordinate> coordinates, List<bool> labels, int numcoords, List<ICoordinate> pivots = null)
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

        private static void RandomizeCoordinatesAndSave(int numcoords, List<ICoordinate> coordinates, Random rnd, List<bool> labels)
        {
            if ((StaticConfigParams.ActionList & Actions.Instance_Uniform) != 0)
            {
                for (var i = 0; i < numcoords; i++)
                {
                    if(StaticConfigParams.RandomInstanceType == typeof(Coordinate))
                        coordinates.Add(Coordinate.MakeRandom());
                    else if (StaticConfigParams.RandomInstanceType == typeof(Coordinate3D))
                        coordinates.Add(Coordinate3D.MakeRandom());
                    labels.Add(rnd.NextDouble() > StaticConfigParams.CONST_NEGATIVELABELRATE);
                }        
            }
            if ((StaticConfigParams.ActionList & Actions.Instance_PlantedSingleEnrichment) != 0)
            {
                for (var i = 0; i < numcoords; i++)
                    if (StaticConfigParams.RandomInstanceType == typeof(Coordinate))
                        coordinates.Add(Coordinate.MakeRandom());
                    else if (StaticConfigParams.RandomInstanceType == typeof(Coordinate))
                        coordinates.Add(Coordinate3D.MakeRandom());
                ICoordinate pivotCoord;
                if (StaticConfigParams.RandomInstanceType == typeof(Coordinate))
                    pivotCoord = Coordinate.MakeRandom();
                else if (StaticConfigParams.RandomInstanceType == typeof(Coordinate3D))
                    pivotCoord = Coordinate3D.MakeRandom();
                var posIds =
                    new HashSet<int>(
                        coordinates.Select((t, idx) => new {Idx = idx, Dist = t.EuclideanDistance(pivotCoord)})
                            .OrderBy(t => t.Dist)
                            .Take((int) ((1- StaticConfigParams.CONST_NEGATIVELABELRATE)*numcoords))
                            .Select(t => t.Idx));
                for (var i = 0; i < numcoords; i++)
                    labels.Add(posIds.Contains(i));
            }
            Generics.SaveToCSV(coordinates.Zip(labels, (a, b) => a.ToString() +","+ Convert.ToDouble(b)),
                $@"coords_{StaticConfigParams.filenamesuffix}.csv");
        }

        private static void PlantEnrichmentAndSave(int numcoords, List<Coordinate> coordinates, Random rnd, List<bool> labels)
        {
            for (var i = 0; i < numcoords; i++)
            {
                coordinates.Add(new Coordinate(rnd.NextDouble(), rnd.NextDouble()));
                var dist = coordinates.Last().EuclideanDistance(new Coordinate(0, 0));
                labels.Add(rnd.NextDouble() > 1.0 - (1.0 / dist)); //verify this
            }
            Generics.SaveToCSV(coordinates.Zip(labels, (a, b) => new[] { a.X, a.Y, Convert.ToDouble(b) }).ToList(), @"coords.csv");
        }

        private static void LoadCoordinatesFromFile(string[] args, ref int numcoords, List<ICoordinate> coordinates,
            List<bool> labels, List<string> identities, Random rnd)
        {
            var lines = File.ReadLines(args[0]).Select(l => l.Split(','));
            numcoords = 0;
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
                    labels.Add(rnd.NextDouble() > 0.5);
                }
            }
        }

        
    }
}
