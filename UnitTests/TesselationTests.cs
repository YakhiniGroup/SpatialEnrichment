using System;
using System.Collections.Generic;
using System.IO;
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
            var res = Program.RandomizeCoordinatesAndSave(20, out var pivot, true);

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
                var res = Program.RandomizeCoordinatesAndSave(20, out var pivot, true);
            }
        }


        [TestMethod]
        public void GridExperiment()
        {
            Program.Config = new ConfigParams("");
            StaticConfigParams.filenamesuffix = "0";
            var instance = Program.RandomizeCoordinatesAndSave(50, out var plantedpivot, true);
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
        public void Empirical3DSamplingTest()
        {
            var filename = @"C:\Users\shaybe\Dropbox\Thesis-PHd\SpatialEnrichment\Caulobacter\transferases\phosphoribosyltransferase_3d.csv";
            var data = File.ReadAllLines(filename).Select(l => l.Split(',')).Select(sl =>
                new Tuple<ICoordinate, bool>(
                    new Coordinate3D(double.Parse(sl[0]), double.Parse(sl[1]), double.Parse(sl[2])),
                    sl[3]=="1")).ToList();
            mHGJumper.Initialize(data.Count(v=>v.Item2), data.Count(v => !v.Item2));
            var empiricalGrid = new Gridding();
            empiricalGrid.GenerateEmpricialDensityGrid(long.MaxValue, data);
            var smph = new SemaphoreSlim(50);
            var mHGval = 1.0;
            var locker = new object();
            foreach (var pivot in empiricalGrid.Pivots.GetConsumingEnumerable())
                Task.Run(() =>
                {
                    smph.WaitAsync();
                    var currval = mHGJumper.minimumHypergeometric(data.OrderBy(c => c.Item1.EuclideanDistance(pivot))
                        .Select(c => c.Item2).ToArray()).Item1;
                    lock (locker)
                    {
                        mHGval = Math.Min(currval, mHGval);
                    }
                    //Console.Write($"\r\r\r\r\r\r\r\r\r\r Pivot seen#{empiricalGrid.NumPivots} curr mHG={mHGval}");
                });
            Console.WriteLine(mHGval);
        }


        [TestMethod]
        public void SamplingGridSearchExperiment()
        {
            Program.Config = new ConfigParams("");
            StaticConfigParams.filenamesuffix = "0";
            var instance = Program.RandomizeCoordinatesAndSave(50, out var plantedpivot, true);
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
