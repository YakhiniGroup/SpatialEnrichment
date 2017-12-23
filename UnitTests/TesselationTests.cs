using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SpatialEnrichment;
using SpatialEnrichment.Helpers;
using SpatialEnrichmentWrapper;

namespace UnitTests
{
    [TestClass]
    public class TesselationTests
    {
        [TestMethod]
        public void VolatileGradientDescent()
        {
            var Config = new ConfigParams("");
            Program.Config = Config;
            var res = Program.RandomizeCoordinatesAndSave(20, true);

            var T = new Tesselation(res.Item1.Select(v => (Coordinate)v).ToList(), res.Item2, null, Config);
            var cell = T.ComputeCellFromCoordinateVolatile(new Coordinate(0.0, 0.0));
            
            Console.WriteLine(cell.CenterOfMass);
        }

        [TestMethod]
        public void SampleProblems()
        {
            Program.Config = new ConfigParams("");
            for (var i = 0; i < 12; i++)
            {
                StaticConfigParams.filenamesuffix = i.ToString();
                var res = Program.RandomizeCoordinatesAndSave(20, true);
            }
        }

        [TestMethod]
        public void SamplingVsGridExperiment()
        {
            Program.Config = new ConfigParams("");
            var vN = new int[] {40, 60, 100};
            var vNu = new int[] {10, 20, 30};
            var res = new Dictionary<Tuple<int, int>, List<double>>();
            foreach (var N in vN)
            {
                foreach (var nu in vNu)
                {
                    res.Add(new Tuple<int, int>(N, nu),  Experiments.CompareExahustiveWithSubsamplingInput(N, nu, 50));
                }
            }

        }

        [TestMethod]
        public void GridExperiment()
        {
            Program.Config = new ConfigParams("");
            StaticConfigParams.filenamesuffix = "0";
            var instance = Program.RandomizeCoordinatesAndSave(50, true);
            var instanceData = instance.Item1.Zip(instance.Item2, (a, b) => new {Coord = a, Label = b}).ToList();
            var uniformGrid = new Gridding();
            uniformGrid.GeneratePivotGrid(1000);
            var grid = uniformGrid.GetPivots().ToList();
            int numcovered = 0;
            double bestP = 1.0;
            Parallel.ForEach(grid, pivot =>
            {
                var boolVec = instanceData.OrderBy(coord=>coord.Coord.EuclideanDistance(pivot)).Select(c=>c.Label).ToArray();
                var res = mHGJumper.minimumHypergeometric(boolVec);
                if (res.Item1 < bestP) bestP = res.Item1;
               // var currid=Interlocked.Increment(ref numcovered);
               // if (currid % 100 == 0)
               //     Console.Write($"\r\r\r\r\r\r\r\r\r\r{currid / grid.Count:P}");
            });
         Console.WriteLine(bestP);
        }

        [TestMethod]
        public void SamplingGridSearchExperiment()
        {
            Program.Config = new ConfigParams("");
            StaticConfigParams.filenamesuffix = "0";
            var instance = Program.RandomizeCoordinatesAndSave(50, true);
            var instanceData = instance.Item1.Zip(instance.Item2, (a, b) => new { Coord = a, Label = b }).ToList();
            var uniformGrid = new Gridding();
            uniformGrid.GeneratePivotGrid(1000);
            var grid = uniformGrid.GetPivots().ToList();
            int numcovered = 0;
            double bestP = 1.0;
            Parallel.ForEach(grid, pivot =>
            {
                var boolVec = instanceData.OrderBy(coord => coord.Coord.EuclideanDistance(pivot)).Select(c => c.Label).ToArray();
                var res = mHGJumper.minimumHypergeometric(boolVec);
                if (res.Item1 < bestP) bestP = res.Item1;
                // var currid=Interlocked.Increment(ref numcovered);
                // if (currid % 100 == 0)
                //     Console.Write($"\r\r\r\r\r\r\r\r\r\r{currid / grid.Count:P}");
            });
            Console.WriteLine(bestP);
        }
    }
}
