using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SpatialEnrichment;
using SpatialEnrichment.Helpers;

namespace SpatialEnrichmentWrapper
{
    public class EnrichmentWrapper
    {
        /// <summary>
        /// Gets as input a list of labeled 2d coordinates as a tuple of (x,y,label).
        /// </summary>
        /// <param name="coordinates">tuple of (x,y,label)</param>
        /// <returns>a list of enriched 2d location centers as SpatialmHGResult</returns>
        public List<SpatialmHGResult> SpatialmHGWrapper(List<Tuple<double, double, bool>> coordinates)
        {
            var ones = coordinates.Sum(t => t.Item3 == true ? 1 : 0);
            var numcoords = coordinates.Count;
            mHGJumper.Initialize(ones, numcoords - ones);
            mHGJumper.optHGT = 0.05;
            var coords = coordinates.Select(t => new Coordinate(t.Item1, t.Item2)).ToList();
            var labels = coordinates.Select(t => t.Item3).ToList();
            var T = new Tesselation(coords, labels, new List<string>());
            var topResults = T.GradientSkippingSweep(numStartCoords: 20, numThreads: Environment.ProcessorCount - 1);
            return topResults.Select(t => new SpatialmHGResult(t)).ToList();
        }
    }

    /// <summary>
    /// Enriched 2d location centers with score and meta data
    /// </summary>
    public class SpatialmHGResult
    {
        //the center of the enrichment "sphere"
        public double X { get; private set; }
        public double Y { get; private set; } 

        public int mHGthreshold { get; private set; } //the number of original labeled points within the enrichment "sphere"
        public double pvalue { get; private set; } //the enrichment score
        public List<Tuple<double, double>> enrichmentPolygon { get; private set; } //the actual bounding polygon vertices (i.e. structure of enrichment "sphere")

        public SpatialmHGResult(Cell c)
        {
            this.pvalue = c.mHG.Item1;
            this.mHGthreshold = c.mHG.Item2;
            this.X = c.CenterOfMass.X;
            this.Y = c.CenterOfMass.Y;
            enrichmentPolygon = new List<Tuple<double, double>>();
            foreach (var coord in c.Coordinates)
            {
                enrichmentPolygon.Add(new Tuple<double, double>(coord.X, coord.Y));
            }
        }

    }
}
