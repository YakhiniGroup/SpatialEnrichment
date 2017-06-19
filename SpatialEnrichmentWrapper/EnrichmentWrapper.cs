using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SpatialEnrichment;
using SpatialEnrichment.Helpers;
using Accord.Statistics.Analysis;

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
            var labels = coordinates.Select(t => t.Item3).ToList();
            var coords = coordinates.Select(t => new Coordinate(t.Item1, t.Item2)).ToList();
            InitializeMHG(labels);
            return Solve2DProblem(coords, labels);
        }

        public List<SpatialmHGResult3D> SpatialmHGWrapper3D(List<Tuple<double, double, double, bool>> input)
        {
            var coordinates = input.Select(c => new Coordinate3D(c.Item1, c.Item2, c.Item3)).ToList();
            var labels = input.Select(c => c.Item4).ToList();
            InitializeMHG(labels);
            int idx = -1;
            var solutions = new List<SpatialmHGResult3D>();
            //Foreach perpendicular bisector plane
            for (var i = 0; i < coordinates.Count; i++)
                for (var j = 0; j < coordinates.Count; j++)
                    if (labels[i] != labels[j])
                    {
                        idx++;
                        //Reduce to 2D problem
                        var plane = Plane.Bisector(coordinates[i], coordinates[j]);
                        if (StaticConfigParams.WriteToCSV)
                            Generics.SaveToCSV(plane, $@"Planes\plane{idx}.csv", true);
                        var subProblemIn2D = plane.ProjectOntoAndRotate(coordinates, out PrincipalComponentAnalysis pca);
                        //Solve 2D problem
                        StaticConfigParams.filenamesuffix = "_"+idx;
                        var res = Solve2DProblem(subProblemIn2D, labels);
                        pca.NumberOfOutputs = 3; //project back to 3D
                        foreach (var mHGresult2D in res)
                            solutions.Add(new SpatialmHGResult3D(mHGresult2D, pca));
                    }
            //Combine 2D solutions
            var combinedResultsNaive = solutions.OrderBy(t => t.pvalue).Take(20).ToList();
            return combinedResultsNaive;
        }

        private static void InitializeMHG(List<bool> labels)
        {
            var ones = labels.Sum(t => t == true ? 1 : 0);
            var numcoords = labels.Count;
            mHGJumper.Initialize(ones, numcoords - ones);
            mHGJumper.optHGT = 0.05;
        }

        private static List<SpatialmHGResult> Solve2DProblem(List<Coordinate> coords, List<bool> labels)
        {
            var T = new Tesselation(coords, labels, new List<string>());
            var topResults = T.GradientSkippingSweep(numStartCoords: 20, numThreads: Environment.ProcessorCount - 1);
            Tesselation.Reset();
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

    public class SpatialmHGResult3D
    {
        //the center of the enrichment "sphere"
        public double X { get; private set; }
        public double Y { get; private set; }
        public double Z { get; private set; }

        public int mHGthreshold { get; private set; } //the number of original labeled points within the enrichment "sphere"
        public double pvalue { get; private set; } //the enrichment score
        public List<Tuple<double, double, double>> enrichmentPolygon { get; private set; } //the actual bounding polygon vertices (i.e. structure of enrichment "sphere")

        public SpatialmHGResult3D(SpatialmHGResult c, PrincipalComponentAnalysis pca)
        {
            this.pvalue = c.pvalue;
            this.mHGthreshold = c.mHGthreshold;
            var reverted_CoM = pca.Revert(new[] { new[] { c.X, c.Y } });
            this.X = reverted_CoM[0][0];
            this.Y = reverted_CoM[0][1];
            this.Z = reverted_CoM[0][2];
            var reverted_enrichPol = pca.Revert(c.enrichmentPolygon.Select(p => new[] { p.Item1, p.Item2 }).ToArray());
            enrichmentPolygon = reverted_enrichPol.Select(v=>new Tuple<double,double,double>(v[0], v[1], v[2])).ToList();
        }
    }
}
