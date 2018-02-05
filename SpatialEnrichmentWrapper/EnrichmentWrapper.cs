using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SpatialEnrichment;
using SpatialEnrichment.Helpers;
using Accord.Statistics.Analysis;
using System.Collections.Concurrent;
using System.Configuration;
using System.IO;
using System.Threading;
using Accord.IO;

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
            List<SpatialmHGResult> solutions;
            Normalizer nrm;
            try
            {
                var labels = coordinates.Select(t => t.Item3).ToList();
                var coords = coordinates.Select(t => (ICoordinate)new Coordinate(t.Item1, t.Item2)).ToList();
                nrm = new Normalizer(coords);
                var normcoords = nrm.Normalize(coords).Select(c=>(Coordinate)c).ToList();
                InitializeMHG(labels);
                solutions = Solve2DProblem(normcoords, labels);
            }
            catch (Exception e)
            {
                Config.Log.WriteLine("Error: {0}", e.Message);
                throw;
            }
            Config.Log.updater?.Wait();
            foreach (var sol in solutions)
                sol.Denormalize(nrm);
            return solutions.Cast<ISpatialmHGResult>().ToList();
        }

        public List<ISpatialmHGResult> SpatialmHGWrapper3D(List<Tuple<double, double, double, bool>> input, bool runViaAzure = false)
        {
            var coordinates = input.Select(c => (ICoordinate)new Coordinate3D(c.Item1, c.Item2, c.Item3)).ToList();
            Normalizer nrm = new Normalizer(coordinates);
            var normcoords = nrm.Normalize(coordinates).Select(c => (Coordinate3D)c).ToList();
            var labels = input.Select(c => c.Item4).ToList();
            InitializeMHG(labels);
            int idx = -1;
            var solutions = new ConcurrentPriorityQueue<double, SpatialmHGResult3D>();
            var planeList = new ConcurrentPriorityQueue<double, Plane>(); //minheap based, smaller is better
            //Foreach perpendicular bisector plane
            for (var i = 0; i < coordinates.Count; i++)
            for (var j = i+1; j < coordinates.Count; j++)
                if (labels[i] != labels[j])
                {
                    //Reduce to 2D problem
                    var plane = Plane.Bisector(normcoords[i], normcoords[j]);
                    planeList.Enqueue(1.0, plane);
                }

            var numPlanes = planeList.Count();

            if ((Config.ActionList & Actions.Search_EmpricalSampling) != 0 || (Config.ActionList & Actions.Search_UniformSampling) != 0)
            {
                var problem = normcoords.Zip(labels, (a, b) => new Tuple<ICoordinate, bool>(a, b)).ToList();
                var gr = new Gridding();
                // var problemSize = MathExtensions.Binomial(numPlanes, 3) + MathExtensions.Binomial(numPlanes, 2) + numPlanes + 1;
                //(long)Math.Min(problemSize, 100000)
                if((Config.ActionList & Actions.Search_EmpricalSampling) != 0)
                    gr.GenerateEmpricialDensityGrid(1000000, problem);
                if((Config.ActionList & Actions.Search_UniformSampling) != 0)
                    gr.GeneratePivotGrid(1000000,3);
                var results = new ConcurrentPriorityQueue<double, ISpatialmHGResult>();
                Parallel.ForEach(gr.GetPivots(), pivot =>
                {
                    var binvec = problem.OrderBy(c => c.Item1.EuclideanDistance(pivot)).Select(c => c.Item2).ToArray();
                    var res = mHGJumper.minimumHypergeometric(binvec);
                    results.Enqueue(res.Item1, new SpatialmHGResult3D(res.Item1, res.Item2, (Coordinate3D)pivot));
                });
                return results.Take(Config.GetTopKResults).Select(v => v.Value).ToList();
            }

            if ((Config.ActionList & Actions.Search_CellSkipping) != 0)
            {
                if (runViaAzure)
                {
                    var fileList = new List<string>();
                    foreach (var file in Directory.EnumerateFiles(@"3D\Planes\")) File.Delete(file);
                    foreach (var file in Directory.EnumerateFiles(@"3D\2dProblems\")) File.Delete(file);
                    var asList = planeList.ToList();
                    Parallel.ForEach(asList, currPlane =>
                    {
                        var currIdx = Interlocked.Increment(ref idx);
                        Console.Write($"\r\r\r\r\r\rGenerating 2D projection {currIdx}/{numPlanes}.");
                        var plane = currPlane.Value;

                        if (StaticConfigParams.WriteToCSV)
                            Config.Log.WriteLine("Selected plane {0}/{1} at distance {2}", currIdx, numPlanes, currPlane.Key);
                        var subProblemIn2D = plane.ProjectOntoAndRotate(normcoords, out PrincipalComponentAnalysis pca);
                        pca.NumberOfOutputs = 3; //project back to 3D
                        pca.Save($@"3D\PCA\pca{currIdx}.bin");
                        Generics.SaveToCSV(plane, $@"3D\Planes\plane{currIdx}.csv", true);
                        Generics.SaveToCSV(subProblemIn2D.Zip(labels, (c, l) => c.ToString() + "," + l), $@"3D\2dProblems\coords{currIdx}.csv", true);
                        fileList.Add($@"3D\2dProblems\coords{currIdx}.csv");
                    });
                    Console.WriteLine(@"Done. Initializing Batch pool.");
                    AzureBatchExecution.MainAsync(fileList).Wait();
                }
                else
                {
                    while (planeList.TryDequeue(out var currPlane))
                    {
                        var plane = currPlane.Value;
                        idx++;
                        if (StaticConfigParams.WriteToCSV)
                            Generics.SaveToCSV(plane, $@"Planes\plane{idx}.csv", true);
                        Config.Log.WriteLine("Selected plane {0}/{1} at distance {2}", idx, numPlanes, currPlane.Key);
                        var subProblemIn2D = plane.ProjectOntoAndRotate(normcoords, out PrincipalComponentAnalysis pca);
                        pca.NumberOfOutputs = 3; //project back to 3D
                                                 //Solve 2D problem
                        StaticConfigParams.filenamesuffix = idx.ToString();
                        var res = Solve2DProblem(subProblemIn2D, labels, normcoords, pca);
                        foreach (var mHGresult2D in res)
                        {
                            var projectedResult = new SpatialmHGResult3D(mHGresult2D, pca, idx);
                            solutions.Enqueue(projectedResult.pvalue, projectedResult);
                        }
                        solutions.TryPeek(out var bestCell);
                        var bestCellCenter = bestCell.Value.GetCenter();
                        var remainingPlanes = planeList.Select(t => t.Value).ToList();
                        planeList.Clear();
                        foreach (var p in remainingPlanes)
                            planeList.Enqueue(bestCellCenter.DistanceToPlane(p), p);
                    }
                }

                //Combine 2D solutions
                var combinedResultsNaive = new List<SpatialmHGResult3D>();
                for (var i = 0; i < Config.GetTopKResults; i++)
                {
                    KeyValuePair<double, SpatialmHGResult3D> bestCell;
                    solutions.TryDequeue(out bestCell);
                    if (bestCell.Key <= Config.SIGNIFICANCE_THRESHOLD)
                    {
                        bestCell.Value.Denormalize(nrm);
                        combinedResultsNaive.Add(bestCell.Value);
                    }
                    else
                        break;
                }
                Config.Log.updater?.Wait();
                return combinedResultsNaive.Cast<ISpatialmHGResult>().ToList();
            }

            return null;
        }

        private static void InitializeMHG(List<bool> labels)
        {
            var ones = labels.Sum(t => t == true ? 1 : 0);
            var numcoords = labels.Count;
            mHGJumper.Initialize(ones, numcoords - ones);
        }


        private List<SpatialmHGResult> Solve2DProblem(List<Coordinate> coords, List<bool> labels, List<Coordinate3D> projectedFrom = null, PrincipalComponentAnalysis pca = null)
        {
            var T = new Tesselation(coords, labels, new List<string>(), Config) { pca = pca };
            if (projectedFrom != null)
                T.ProjectedFrom = projectedFrom.Cast<ICoordinate>().ToList();

            IEnumerable<Cell> topResults = null;
            if ((Config.ActionList & Actions.Search_CellSkipping) != 0)
            {
                topResults = T.GradientSkippingSweep(numStartCoords: 20, numThreads: Environment.ProcessorCount - 1);
                Tesselation.Reset();
                return topResults.Select(t => new SpatialmHGResult(t)).ToList();
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
            if ((Config.ActionList & Actions.Search_EmpricalSampling) != 0)
            {
                var problem = coords.Zip(labels, (a, b) => new Tuple<ICoordinate, bool>(a, b)).ToList();
                var gr = new Gridding();
                var problemSize = MathExtensions.Binomial(Line.Count, 2) + Line.Count + 1;
                gr.GenerateEmpricialDensityGrid((long)Math.Min(problemSize, 100000), problem);
                var results = new ConcurrentPriorityQueue<double, SpatialmHGResult>();
                Parallel.ForEach(gr.GetPivots(), pivot =>
                {
                    var binvec = problem.OrderBy(c => c.Item1.EuclideanDistance(pivot)).Select(c => c.Item2).ToArray();
                    var res = mHGJumper.minimumHypergeometric(binvec);
                    results.Enqueue(res.Item1, new SpatialmHGResult(res.Item1, res.Item2, (Coordinate)pivot));
                    while (results.Count > Config.GetTopKResults)
                        results.TryDequeue(out var junk);
                });
                return results.Select(v => v.Value).ToList();
            }

            return null;
        }

        public List<ISpatialmHGResult> mHGPivotWrapper(List<Tuple<double, double, bool>> coordinates, List<Tuple<double,double>> pivots = null)
        {
            var cpivots = pivots == null ? coordinates.Select(c => new Coordinate(c.Item1, c.Item2)).ToList() : pivots.Select(c => new Coordinate(c.Item1, c.Item2)).ToList();
            var labeledCoords = coordinates.Select(c => new { Coord = new Coordinate(c.Item1, c.Item2), Label = c.Item3 }).ToList();
            InitializeMHG(labeledCoords.Select(c=>c.Label).ToList());
            var results = new ConcurrentBag<SpatialmHGResult>();
            Parallel.ForEach(cpivots, piv =>
            {
                var ordDat = labeledCoords.OrderBy(c => piv.EuclideanDistance(c.Coord)).ToList();
                var vec = ordDat.Select(c => c.Label).ToArray();
                var res = mHGJumper.minimumHypergeometric(vec);
                results.Add(new SpatialmHGResult(res.Item1, res.Item2, piv));
            });
            return results.Where(r=>r != null).OrderBy(r=>r.pvalue).Take(Config.GetTopKResults).Cast<ISpatialmHGResult>().ToList();
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

        public SpatialmHGResult(double pval, int thresh, Coordinate pos)
        {
            this.pvalue = pval;
            this.mHGthreshold = thresh;
            this.X = pos.X;
            this.Y = pos.Y;
            enrichmentPolygon = new List<Tuple<double, double>>();
        }

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

        public void Denormalize(Normalizer nrm)
        {
            var nc = nrm.DeNormalize(new Coordinate(X, Y));
            X = nc.GetDimension(0);
            Y = nc.GetDimension(1);
            enrichmentPolygon = enrichmentPolygon.Select(p => nrm.DeNormalize(new Coordinate(p.Item1, p.Item2))).Select(v => new Tuple<double, double>(v.GetDimension(0), v.GetDimension(1))).ToList();
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
        public SpatialmHGResult3D(double pval, int thresh, Coordinate3D pos)
        {
            pvalue = pval;
            mHGthreshold = thresh;
            X = pos.X;
            Y = pos.Y;
            Z = pos.Z;
            enrichmentPolygon =
                new List<Tuple<double, double, double>>() {new Tuple<double, double, double>(pos.X, pos.Y, pos.Z)};
        }
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

        public void Denormalize(Normalizer nrm)
        {
            var nc = nrm.DeNormalize(new Coordinate3D(X, Y, Z));
            X = nc.GetDimension(0);
            Y = nc.GetDimension(1);
            Z = nc.GetDimension(2);
            enrichmentPolygon = enrichmentPolygon.Select(p => nrm.DeNormalize(new Coordinate3D(p.Item1, p.Item2, p.Item3))).Select(v => new Tuple<double, double, double>(v.GetDimension(0), v.GetDimension(1), v.GetDimension(2))).ToList();
        }

        public override string ToString()
        {
            return $"{X},{Y},{Z},{pvalue}";
        }
    }


    public interface ISpatialmHGResult
    {
        void SaveToCSV(string csv);
        void Denormalize(Normalizer nrm);
    }

    
}
