using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SpatialEnrichment;

namespace SpatialEnrichmentWrapper
{
    public static class Gridding
    {
        public static IEnumerable<Coordinate> GeneratePivotGrid(int numsamples)
        {
            var resolution = Math.Sqrt(numsamples);
            const double buffer = 0.1;
            for (var i = -buffer; i < 1 + buffer; i += 1.0 / resolution)
            for (var j = -buffer; j < 1 + buffer; j += 1.0 / resolution)
                yield return new Coordinate(i, j);
        }

        public static IEnumerable<Coordinate> GenerateEmpricialDensityGrid(int numsamples, List<Coordinate> data)
        {
            var pairs = (from a in data from b in data where !a.Equals(b) select new Tuple<Coordinate, Coordinate>(a, b)).ToList();

            for (var i = 0; i < numsamples; i++)
            {
                var pair = pairs.OrderBy(v => StaticConfigParams.rnd.NextDouble()).First();
                var line = Line.Bisector(pair.Item1, pair.Item2);
                
                line.Perpendicular()


            }

            var firstperm = data.OrderBy(a => StaticConfigParams.rnd.NextDouble()).ToList();
            var firstenum = firstperm.GetEnumerator();
            var secperm = data.OrderBy(a => StaticConfigParams.rnd.NextDouble()).ToList();
            var secenum = secperm.GetEnumerator();
            var res = new List<Coordinate>();
            for (var i = 0; i < numsamples; i++)
            {
                firstenum.MoveNext();
            }
            firstenum.Dispose();
            secenum.Dispose();
        }
    }
}
