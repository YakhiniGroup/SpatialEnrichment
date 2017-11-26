using System;
using System.Linq;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SpatialEnrichment;
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
    }
}
