using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SpatialEnrichment;
using System.Collections.Concurrent;

namespace SpatialEnrichmentWrapper
{
    public static class Gridding
    {
        public static IEnumerable<Coordinate> GeneratePivotGrid(long numsamples)
        {
            var resolution = Math.Sqrt(numsamples);
            const double buffer = 0.1;
            for (var i = -buffer; i < 1 + buffer; i += 1.0 / resolution)
            for (var j = -buffer; j < 1 + buffer; j += 1.0 / resolution)
                yield return new Coordinate(i, j);
        }

        public static IEnumerable<Coordinate> GenerateEmpricialDensityGrid(long numsamples, List<Tuple<Coordinate,bool>> lableddata, double jitterscale = 1E-5)
        {
            var pairs = (from a in lableddata from b in lableddata
                         where !a.Item1.Equals(b.Item1) && !a.Item2.Equals(b.Item2)
                         select new Tuple<Coordinate, Coordinate>(a.Item1, b.Item1)).ToList();

            var resqueue = new BlockingCollection<Coordinate>(10000);

            //Producer
            Task.Run(() => {
                Parallel.For(0, numsamples, (i) =>
                {
                    var quartet = pairs.OrderBy(v => StaticConfigParams.rnd.NextDouble()).Take(2).ToList();
                    var firstpair = quartet.First();
                    var firstbisectorLine = Line.Bisector(firstpair.Item1, firstpair.Item2, isCounted: false);
                    var secondpair = quartet.Last();
                    var secondbisectorLine = Line.Bisector(secondpair.Item1, secondpair.Item2, isCounted: false);
                    var intersectionCoord = firstbisectorLine.Intersection(secondbisectorLine);
                    
                    var jitteredPivot = new Coordinate(
                        intersectionCoord.X + GeometryHelpers.SampleGaussian(StaticConfigParams.rnd, 0.0, jitterscale),
                        intersectionCoord.Y + GeometryHelpers.SampleGaussian(StaticConfigParams.rnd, 0.0, jitterscale));

                    resqueue.Add(jitteredPivot);
                });
                resqueue.CompleteAdding();
            });
            foreach (var item in resqueue.GetConsumingEnumerable())
                yield return item;
        }

    }
}
