using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SpatialEnrichment.Helpers;
using SpatialEnrichmentWrapper;

namespace SpatialEnrichment
{
    public static class Debugging
    {
        public static void debug_mHG(int N, int Ones)
        {
            bool[] bvec = new bool[N];
            for (var i = 0; i < Ones; i++)
                bvec[i] = true;
            //Console.WriteLine(@"Strong p={0}", mHG.minimumHypergeometric(bvec, -1, -1, Program.CorrectionType));
            Console.WriteLine(@"Strong p={0}", mHGJumper.minimumHypergeometric(bvec, -1, -1, StaticConfigParams.CorrectionType));
            //var worst1 = mHG.minimumHypergeometric(bvec.Reverse().ToArray(), -1, -1, Program.CorrectionType);
            var worst2 = mHGJumper.minimumHypergeometric(bvec.Reverse().ToArray(), -1, -1, StaticConfigParams.CorrectionType);
            Console.WriteLine(@"Weak p2={0}", worst2);
            var reslst1 = new List<double>();
            var reslst2 = new List<double>();
            int numBins = 20;
            int[] resbuckets1 = new int[numBins];
            int[] resbuckets2 = new int[numBins];
            for (var i = 0; i < 10000; i++)
            {
                var tvec = bvec.OrderBy(t => StaticConfigParams.rnd.NextDouble()).ToArray();
                //var res1 = mHG.minimumHypergeometric(tvec, -1, -1, Program.CorrectionType);
                var res2 = mHGJumper.minimumHypergeometric(tvec, -1, -1, StaticConfigParams.CorrectionType);
                //reslst1.Add(res1.Item1);
                reslst2.Add(res2.Item1);
                /*
                if (res1.Item1 > worst1.Item1)
                {
                    Console.WriteLine(string.Join(" ", tvec.Select(Convert.ToInt32)));
                    Console.WriteLine();
                }
                */
                if(res2.Item1 >= 1 || res2.Item1 <= 0)
                    Console.WriteLine(string.Join(" ",tvec));
                //resbuckets1[Math.Min((int)Math.Floor(res1.Item1 * numBins), numBins - 1)]++;
                resbuckets2[Math.Min((int)Math.Floor(res2.Item1 * numBins), numBins - 1)]++;
            }
            //Console.WriteLine(string.Join(" - ", resbuckets1));
            Console.WriteLine(string.Join(" - ", resbuckets2));
            Console.WriteLine("Max1 = {0}, Max2 = {1}", reslst1.Max(),reslst2.Max());
        }
    }
}
