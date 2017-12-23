using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SpatialEnrichment;
using System.Collections.Concurrent;
using System.Threading;

namespace SpatialEnrichmentWrapper
{
    public class Gridding
    {
        private Task producer;
        private BlockingCollection<Coordinate> pivots;
        private IEnumerator<Coordinate> elementEnumerator;
        public Coordinate NextPivot => elementEnumerator.MoveNext() ? elementEnumerator.Current : null;

        public Gridding()
        {
            pivots = new BlockingCollection<Coordinate>(10000);
            elementEnumerator = pivots.GetConsumingEnumerable().GetEnumerator();
        }

        public void GeneratePivotGrid(long numsamples)
        {
            producer = Task.Run(() =>
            {
                var resolution = Math.Sqrt(numsamples);
                const double buffer = 0.1;
                for (var i = -buffer; i < 1 + buffer; i += 1.0 / resolution)
                for (var j = -buffer; j < 1 + buffer; j += 1.0 / resolution)
                    pivots.Add(new Coordinate(i, j));
                pivots.CompleteAdding();
            });
        }

        public void GenerateEmpricialDensityGrid(long numsamples, List<Tuple<Coordinate,bool>> lableddata, double jitterscale = 1E-5)
        {
            producer = Task.Run(() =>
            {
                var pairs = new List<Tuple<Coordinate, Coordinate>>();
                for (var i = 0; i < lableddata.Count-1; i++)
                for (var j = i+1; j < lableddata.Count; j++)
                    if (lableddata[i].Item2 != lableddata[j].Item2)
                        pairs.Add(new Tuple<Coordinate, Coordinate>(lableddata[i].Item1, lableddata[j].Item1));
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

                    pivots.Add(jitteredPivot);
                });
                pivots.CompleteAdding();
            });
        }

        public IEnumerable<Coordinate> GetPivots()
        {
            foreach (var el in pivots.GetConsumingEnumerable())
                yield return el;
        }

    }
}
