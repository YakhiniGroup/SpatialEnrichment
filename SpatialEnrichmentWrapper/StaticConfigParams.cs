using System;
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
        Search_Originals = 1 << 3,                  //Covers all original points as potential pivots
        Search_CoordinateSample = 1 << 4,          //Finds cell by sample coordinate
        Search_GradientDescent = 1 << 5,            //Only crosses into better cells
        Program_RandomConstSeed = 1 << 6,           //If used Const for seed 
        Search_SimulatedAnnealing = 1 << 7,
        Search_FixedSet = 1 << 8,
        Search_LineSweep = 1 << 9
    }

    public static class StaticConfigParams
    {
        public static bool WriteToCSV = false; //writes cell to files
        public const Actions ActionList =
            Actions.Program_RandomConstSeed |
            Actions.Instance_Uniform |
            //Actions.Search_Originals;
            //Actions.Search_Exhaustive;
            //Actions.Search_LineSweep;
            //Actions.Search_FixedSet;
            Actions.Search_CoordinateSample | Actions.Search_GradientDescent;

        public static Stopwatch timer = new Stopwatch();
        public const double TOLERANCE = 1E-10;
        public const double CONST_NEGATIVELABELRATE = 0.75;
        public const double ExploreExploitRatio = 0.9;
        public const double CONST_SIGNIFICANCE_THRESHOLD = 0.05;
        public const int CONST_CONCURRENCY = 30;
        public const int CONST_SKIP_SLACK = 0; // gradient skipping slack parameter. negative yields more cells.
        public static mHGCorrectionType CorrectionType = mHGCorrectionType.Exact;
        public static Type RandomInstanceType = typeof(Coordinate3D);
        public static bool ComputeSanityChecks = false;
        public static string filenamesuffix = "";
        public static Random rnd = new Random();
        public static double Cellcount;
        public static int GetTopKResults = 100;
    }
}
