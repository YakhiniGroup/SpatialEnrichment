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
        private BlockingCollection<ICoordinate> pivots;
        private IEnumerator<ICoordinate> elementEnumerator;
        public ICoordinate NextPivot => elementEnumerator.MoveNext() ? elementEnumerator.Current : null;

        public Gridding()
        {
            pivots = new BlockingCollection<ICoordinate>(10000);
            elementEnumerator = pivots.GetConsumingEnumerable().GetEnumerator();
        }

        public void GeneratePivotGrid(long numsamples, int dim=2)
        {
            producer = Task.Run(() =>
            {
                var resolution = Math.Pow(numsamples, 1.0/dim);
                const double buffer = 0.1;
                for (var i = -buffer; i < 1 + buffer; i += 1.0 / resolution)
                for (var j = -buffer; j < 1 + buffer; j += 1.0 / resolution)
                    switch (dim)
                    {
                        case 2:
                            pivots.Add(new Coordinate(i, j));
                            break;
                        case 3:
                            for (var k = -buffer; k < 1 + buffer; k += 1.0 / resolution)
                                pivots.Add(new Coordinate3D(i, j, k));
                            break;
                    }
                
                pivots.CompleteAdding();
            });
        }

        public void GenerateEmpricialDensityGrid(long numsamples, List<Tuple<ICoordinate,bool>> lableddata, double jitterscale = 1E-7)
        {
            producer = Task.Run(() =>
            {
                var pairs = new List<Tuple<ICoordinate, ICoordinate>>();
                for (var i = 0; i < lableddata.Count-1; i++)
                for (var j = i+1; j < lableddata.Count; j++)
                    if (lableddata[i].Item2 != lableddata[j].Item2)
                        pairs.Add(new Tuple<ICoordinate, ICoordinate>(lableddata[i].Item1, lableddata[j].Item1));
                //add bottom boundary
                var problemDim = lableddata.First().Item1.GetDimensionality();
                switch (problemDim)
                {
                    case 2:
                        pairs.Add(new Tuple<ICoordinate, ICoordinate>(new Coordinate(0, 0), new Coordinate(jitterscale, -20)));
                        break;
                    case 3:
                        pairs.Add(new Tuple<ICoordinate, ICoordinate>(new Coordinate3D(0, 0, 0), new Coordinate3D(jitterscale, jitterscale, -20)));
                        break;
                }
                
                Parallel.For(0, numsamples, (i) =>
                {
                    var inducers = pairs.OrderBy(v => StaticConfigParams.rnd.NextDouble()).Take(problemDim).ToList();
                    Coordinate intersectionCoord;
                    ICoordinate jitteredPivot = null;
                    switch (problemDim)
                    {
                        case 2:
                            var firstbisectorLine = Line.Bisector((Coordinate)inducers[0].Item1, (Coordinate)inducers[0].Item2, isCounted: false);
                            var secondbisectorLine = Line.Bisector((Coordinate)inducers[1].Item1, (Coordinate)inducers[1].Item2, isCounted: false);
                            intersectionCoord = firstbisectorLine.Intersection(secondbisectorLine);
                            jitteredPivot = new Coordinate(intersectionCoord.X, intersectionCoord.Y + jitterscale);
                            break;
                        case 3:
                            var firstbisectorPlane = Plane.Bisector((Coordinate3D)inducers[0].Item1, (Coordinate3D)inducers[0].Item2);
                            var secondbisectorPlane = Plane.Bisector((Coordinate3D)inducers[1].Item1, (Coordinate3D)inducers[1].Item2);
                            var thirdbisectorPlane = Plane.Bisector((Coordinate3D)inducers[2].Item1, (Coordinate3D)inducers[2].Item2);
                            var firstplaneIntersection = firstbisectorPlane.PlaneIntersection(secondbisectorPlane);
                            var secondplaneIntersection = firstbisectorPlane.PlaneIntersection(thirdbisectorPlane);
                            intersectionCoord = firstplaneIntersection.Intersection(secondplaneIntersection);
                            jitteredPivot = new Coordinate(intersectionCoord.X, intersectionCoord.Y + jitterscale);
                            break;
                    }
                    /*
                    var jitteredPivot = new Coordinate(
                        intersectionCoord.X + GeometryHelpers.SampleGaussian(StaticConfigParams.rnd, 0.0, jitterscale),
                        intersectionCoord.Y + GeometryHelpers.SampleGaussian(StaticConfigParams.rnd, 0.0, jitterscale));
                    */
                    
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
