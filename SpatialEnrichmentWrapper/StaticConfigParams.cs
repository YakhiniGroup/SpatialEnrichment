using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SpatialEnrichment;
using SpatialEnrichment.Helpers;

namespace SpatialEnrichmentWrapper
{
    [Flags]
    public enum Actions
    {
        None = 0,
        Instance_Uniform = 1 << 0,                  //Generates instance by random uniform assignment of label
        Instance_PlantedSingleEnrichment = 1 << 1,  //Deterministic and plants all 1's around one pivot
        Search_Exhaustive = 1 << 2,                 //Covers all cells
        Search_Originals = 1 << 3,                  //Covers all original points as potential Pivots
        Search_CellSkipping = 1 << 4,          //Finds cell by sample coordinate
        Search_GradientDescent = 1 << 5,            //Only crosses into better cells
        Program_RandomConstSeed = 1 << 6,           //If used Const for seed 
        Search_SimulatedAnnealing = 1 << 7,
        Search_FixedSet = 1 << 8,
        Search_LineSweep = 1 << 9,
        Search_EmpricalSampling = 1 << 10,
        Search_UniformSampling = 1 << 11,
        Filter_DegenerateLines = 1 << 12,
        Experiment_ComparePivots = 1 << 13,
        Experiment_SampleLines = 1 << 14
    }

    public enum SamplingType
    {
        Pivot,
        Sampling,
        Grid
    }

    public static class StaticConfigParams
    {
        public static bool WriteToCSV = false; //writes cell to files
        public const double TOLERANCE = 1E-8;
        public const double CONST_PROBLEM_SCALE = 100;
        public const double CONST_NEGATIVELABELRATE = 0.7;
        public const double ExploreExploitRatio = 0.9;
        public const int CONST_CONCURRENCY = 30;
        public static mHGCorrectionType CorrectionType = mHGCorrectionType.Exact;
        public static Type RandomInstanceType = typeof(Coordinate); //Coordinate3D
        public static bool ComputeSanityChecks = false;
        public static string filenamesuffix = "";
        public static SafeRandom rnd = new SafeRandom();
    }

    public class ConfigParams
    {
        //configuration parameters
        public LogWrapper Log;
        public int SKIP_SLACK = -30; // gradient skipping slack parameter. negative yields more cells. needs to be -1 due to tie-breaking of equi-scored cells!
        public double SIGNIFICANCE_THRESHOLD = 0.05;
        public int GetTopKResults = 10;
        public double FilterKFurthestZeros = 0.0; //% of 0's to throw away from data

        public Actions ActionList =
            //Actions.Experiment_ComparePivots |
            //Actions.Experiment_SampleLines | 
            //Actions.Program_RandomConstSeed |
            Actions.Instance_PlantedSingleEnrichment |
            //Actions.Instance_Uniform |
            Actions.Filter_DegenerateLines | // Warning, this might not work.
                                             //Actions.Search_Originals;
                                             //Actions.Search_Exhaustive;
                                             //Actions.Search_LineSweep;
                                             //Actions.Search_FixedSet;
                                             //Actions.Search_CellSkipping | Actions.Search_GradientDescent;
            //Actions.Search_UniformSampling;
            Actions.Search_EmpricalSampling;
        //non-parameters
        public double Cellcount;
        public ConcurrentBag<Tuple<double, int>> mHGlist = new ConcurrentBag<Tuple<double, int>>();
        public int computedMHGs;
        public Stopwatch timer = new Stopwatch();

        public ConfigParams(string tokenId = "")
        {
            this.Log = new LogWrapper(tokenId);
        }

        public ConfigParams(Dictionary<string, string> fromDict)
        {
            //TODO: improve this CTOR - exceptions
            string actionValue;
            string skipSlackValue;
            string thresholdValue;
            string executionTokenIdValue;

            fromDict.TryGetValue("Action", out actionValue);
            fromDict.TryGetValue("SKIP_SLACK", out skipSlackValue);
            fromDict.TryGetValue("SIGNIFICANCE_THRESHOLD", out thresholdValue);
            fromDict.TryGetValue("ExecutionTokenId", out executionTokenIdValue);

            Enum.TryParse(actionValue, out this.ActionList);
            int.TryParse(skipSlackValue, out this.SKIP_SLACK);
            double.TryParse(thresholdValue, out this.SIGNIFICANCE_THRESHOLD);
            this.Log = new LogWrapper(executionTokenIdValue);
        }
    }
}
