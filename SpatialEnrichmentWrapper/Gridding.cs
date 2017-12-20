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

        public static IEnumerable<Coordinate> GenerateEmpricialDensityGrid(long numsamples, List<Tuple<Coordinate,bool>> lableddata, double jitterscale = 0.0001)
        {
            var pairs = (from a in lableddata from b in lableddata
                         where !a.Item1.Equals(b.Item1) && !a.Item2.Equals(b.Item2)
                         select new Tuple<Coordinate, Coordinate>(a.Item1, b.Item1)).ToList();

            var extendedPairs = new List<Tuple<Coordinate, Coordinate, int>>();
            for (var i = 0; i < 4; i++)
                foreach (var pair in pairs)
                    extendedPairs.Add(new Tuple<Coordinate, Coordinate, int>(pair.Item1, pair.Item2, i));


            var resqueue = new BlockingCollection<Coordinate>(10000);

            var producer = Task.Run(() => {
                Parallel.For(0, numsamples, (i) => {
                    var pair = extendedPairs.OrderBy(v => StaticConfigParams.rnd.NextDouble()).First();
                    var bisectorLine = Line.Bisector(pair.Item1, pair.Item2);
                    var perpendicularLine = bisectorLine.Perpendicular(pair.Item1);
                    var intersectionCoord = bisectorLine.Intersection(perpendicularLine);
                    Coordinate jitteredPivot = null;
                    
                    switch (pair.Item3) //determine jitter directionality
                    {
                        case 0:
                            jitteredPivot = new Coordinate(intersectionCoord.X + StaticConfigParams.rnd.NextDouble() * jitterscale, intersectionCoord.Y + StaticConfigParams.rnd.NextDouble() * jitterscale);
                            break;
                        case 1:
                            jitteredPivot = new Coordinate(intersectionCoord.X + StaticConfigParams.rnd.NextDouble() * jitterscale, intersectionCoord.Y - StaticConfigParams.rnd.NextDouble() * jitterscale);
                            break;
                        case 2:
                            jitteredPivot = new Coordinate(intersectionCoord.X - StaticConfigParams.rnd.NextDouble() * jitterscale, intersectionCoord.Y + StaticConfigParams.rnd.NextDouble() * jitterscale);
                            break;
                        case 3:
                            jitteredPivot = new Coordinate(intersectionCoord.X - StaticConfigParams.rnd.NextDouble() * jitterscale, intersectionCoord.Y - StaticConfigParams.rnd.NextDouble() * jitterscale);
                            break;
                    }
                    resqueue.Add(jitteredPivot);
                });
                resqueue.CompleteAdding();
            });
            foreach (var item in resqueue.GetConsumingEnumerable())
                yield return item;
        }
    }
}
