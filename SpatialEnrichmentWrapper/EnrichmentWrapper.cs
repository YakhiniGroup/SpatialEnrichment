using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SpatialEnrichment;
using SpatialEnrichment.Helpers;
using Accord.Statistics.Analysis;
using System.Collections.Concurrent;

namespace SpatialEnrichmentWrapper
{
    public class EnrichmentWrapper
    {
        ConfigParams Config;
        public EnrichmentWrapper(Dictionary<string,string> cnf)
        {
            Config = new ConfigParams(cnf);
        }
        public EnrichmentWrapper(ConfigParams cnf)
        {
            Config = cnf;
        }


        /// <summary>
        /// Gets as input a list of labeled 2d coordinates as a tuple of (x,y,label).
        /// </summary>
        /// <param name="coordinates">tuple of (x,y,label)</param>
        /// <returns>a list of enriched 2d location centers as SpatialmHGResult</returns>
        public List<ISpatialmHGResult> SpatialmHGWrapper(List<Tuple<double, double, bool>> coordinates)
        {
            var labels = coordinates.Select(t => t.Item3).ToList();
            var coords = coordinates.Select(t => new Coordinate(t.Item1, t.Item2)).ToList();
            InitializeMHG(labels);
            var solutions = Solve2DProblem(coords, labels);
            return solutions.Cast<ISpatialmHGResult>().ToList();
        }

        public List<ISpatialmHGResult> SpatialmHGWrapper3D(List<Tuple<double, double, double, bool>> input)
        {
            var coordinates = input.Select(c => new Coordinate3D(c.Item1, c.Item2, c.Item3)).ToList();
            var labels = input.Select(c => c.Item4).ToList();
            InitializeMHG(labels);
            int idx = -1;
            var solutions = new ConcurrentPriorityQueue<double, SpatialmHGResult3D>();
            var planeList = new ConcurrentPriorityQueue<double, Plane>(); //minheap based, smaller is better
            //Foreach perpendicular bisector plane
            for (var i = 0; i < coordinates.Count; i++)
                for (var j = 0; j < coordinates.Count; j++)
                    if (labels[i] != labels[j])
                    {
                        //Reduce to 2D problem
                        var plane = Plane.Bisector(coordinates[i], coordinates[j]);
                        planeList.Enqueue(1.0, plane);
                    }

            var numPlanes = planeList.Count();
            KeyValuePair<double, Plane> currPlane;
            while (planeList.TryDequeue(out currPlane))
            {
                var plane = currPlane.Value;
                idx++;
                if (StaticConfigParams.WriteToCSV)
                    Generics.SaveToCSV(plane, $@"Planes\plane{idx}.csv", true);
                Console.WriteLine("Selected plane {0}/{1} at distance {2}",idx, numPlanes, currPlane.Key);
                var subProblemIn2D = plane.ProjectOntoAndRotate(coordinates, out PrincipalComponentAnalysis pca);
                pca.NumberOfOutputs = 3; //project back to 3D
                //Solve 2D problem
                StaticConfigParams.filenamesuffix = idx.ToString();
                var res = Solve2DProblem(subProblemIn2D, labels, coordinates, pca);
                foreach (var mHGresult2D in res)
                {
                    var projectedResult = new SpatialmHGResult3D(mHGresult2D, pca, idx);
                    solutions.Enqueue(projectedResult.pvalue, projectedResult);
                }
                KeyValuePair<double, SpatialmHGResult3D> bestCell;
                solutions.TryPeek(out bestCell);
                var bestCellCenter = bestCell.Value.GetCenter();
                var remainingPlanes = planeList.Select(t => t.Value).ToList();
                planeList.Clear();
                foreach (var p in remainingPlanes)
                    planeList.Enqueue(bestCellCenter.DistanceToPlane(p), p);
            }

            //Combine 2D solutions
            var combinedResultsNaive = new List<SpatialmHGResult3D>();
            for (var i=0; i< Config.GetTopKResults; i++)
            {
                KeyValuePair<double, SpatialmHGResult3D> bestCell;
                solutions.TryDequeue(out bestCell);
                if (bestCell.Key <= Config.SIGNIFICANCE_THRESHOLD)
                    combinedResultsNaive.Add(bestCell.Value);
                else
                    break;
            }
            return combinedResultsNaive.Cast<ISpatialmHGResult>().ToList();
        }

        private static void InitializeMHG(List<bool> labels)
        {
            var ones = labels.Sum(t => t == true ? 1 : 0);
            var numcoords = labels.Count;
            mHGJumper.Initialize(ones, numcoords - ones);
            mHGJumper.optHGT = 0.05;
        }

        private List<SpatialmHGResult> Solve2DProblem(List<Coordinate> coords, List<bool> labels, List<Coordinate3D> projectedFrom = null, PrincipalComponentAnalysis pca = null)
        {
            var T = new Tesselation(coords, labels, new List<string>(), Config) { pca = pca };
            if (projectedFrom != null)
                T.ProjectedFrom = projectedFrom.Cast<ICoordinate>().ToList();

            IEnumerable<Cell> topResults = null;
            if ((Config.ActionList & Actions.Search_CoordinateSample) != 0)
            {
                topResults = T.GradientSkippingSweep(numStartCoords: 20, numThreads: Environment.ProcessorCount - 1);
            }
            if ((Config.ActionList & Actions.Search_Exhaustive) != 0)
            {
                T.GenerateFromCoordinates();
            }
            if ((Config.ActionList & Actions.Search_Originals) != 0)
            {
                //mHGOnOriginalPoints(args, coordinates, labels, numcoords);
            }
            if ((Config.ActionList & Actions.Search_FixedSet) != 0)
            {
                /*
                var avgX = coordinates.Select(c => c.GetDimension(0)).Average();
                var avgY = coordinates.Select(c => c.GetDimension(1)).Average();
                var cord = new Coordinate(avgX, avgY);
                mHGOnOriginalPoints(args, coordinates, labels, numcoords, new List<ICoordinate>() { cord });
                */
            }
            if ((Config.ActionList & Actions.Search_LineSweep) != 0)
            {
                T.LineSweep();
            }
            
            Tesselation.Reset();
            return topResults.Select(t => new SpatialmHGResult(t)).ToList();
        }
    }

    /// <summary>
    /// Enriched 2d location centers with score and meta data
    /// </summary>
    public class SpatialmHGResult : ISpatialmHGResult
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
        public void SaveToCSV(string filename)
        {
            var toSave = new List<double[]>() { new[] { pvalue, mHGthreshold } };
            toSave.AddRange(enrichmentPolygon.Select(p => new[] { p.Item1, p.Item2 }));
            toSave.Add(new[] { enrichmentPolygon.First().Item1, enrichmentPolygon.First().Item2 });
            Generics.SaveToCSV(toSave, filename, true);
        }
    }

    public class SpatialmHGResult3D : ISpatialmHGResult
    {
        //the center of the enrichment "sphere"
        public double X { get; private set; }
        public double Y { get; private set; }
        public double Z { get; private set; }

        public int mHGthreshold { get; private set; } //the number of original labeled points within the enrichment "sphere"
        public double pvalue { get; private set; } //the enrichment score
        public List<Tuple<double, double, double>> enrichmentPolygon { get; private set; } //the actual bounding polygon vertices (i.e. structure of enrichment "sphere")

        private int PlaneId;
        public SpatialmHGResult3D(SpatialmHGResult c, PrincipalComponentAnalysis pca, int planeId = -1)
        {
            PlaneId = planeId;
            this.pvalue = c.pvalue;
            this.mHGthreshold = c.mHGthreshold;
            var reverted_CoM = pca.Revert(new[] { new[] { c.X, c.Y } });
            this.X = reverted_CoM[0][0];
            this.Y = reverted_CoM[0][1];
            this.Z = reverted_CoM[0][2];
            var reverted_enrichPol = pca.Revert(c.enrichmentPolygon.Select(p => new[] { p.Item1, p.Item2 }).ToArray());
            enrichmentPolygon = reverted_enrichPol.Select(v=>new Tuple<double,double,double>(v[0], v[1], v[2])).ToList();
        }

        public Coordinate3D GetCenter()
        {
            return new Coordinate3D(X, Y, Z);
        }

        public void SaveToCSV(string filename)
        {
            var toSave = new List<double[]>() { new[] { pvalue, mHGthreshold } };
            toSave.AddRange(enrichmentPolygon.Select(p => new[] { p.Item1, p.Item2, p.Item3 }));
            toSave.Add(new[] { enrichmentPolygon.First().Item1, enrichmentPolygon.First().Item2, enrichmentPolygon.First().Item3 });
            Generics.SaveToCSV(toSave, filename, true);
        }
    }


    public interface ISpatialmHGResult
    {
        void SaveToCSV(string csv);
    }
}
