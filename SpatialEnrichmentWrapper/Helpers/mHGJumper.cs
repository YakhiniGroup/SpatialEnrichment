using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Remoting.Messaging;
using System.Threading;
using System.Threading.Tasks;
using SpatialEnrichmentWrapper;

namespace SpatialEnrichment.Helpers
{
    public enum mHGCorrectionType { Exact, Lipson, Bonferroni, None }
    /// <summary>
    /// This class computes mHG efficiently for traversal of the spatial enrichment space.
    /// It can also offer precomputed jump distances given an mHG score to the nearest possible neighbor
    /// with a better score.
    /// </summary>
    public static class mHGJumper
    {
        /// <summary>
        /// Algorithm details available here http://www.ploscompbiol.org/article/fetchObject.action?uri=info:doi/10.1371/journal.pcbi.0030039&representation=PDF
        /// </summary>
        /// <param name="binVec">relevant vector subset (topK from original)</param>
        /// <param name="tN">universe size (length of original vector)</param>
        /// <param name="tB">|falses|</param>
        /// <returns>mHG p-Value and threshold</returns>
        private const int c_rZone = 0;
        public static readonly double Epsilon = 0.01;
        private static double[,] HGTmat;
        
        private static object concLocker = new object();
        public static double? optHGT = 0.05;

        private static double TotalPaths;
        public static int Ones, Zeros;
        public static int Lines => Ones * Zeros;
        private static ConcurrentDictionary<double, double> ScoreMap = new ConcurrentDictionary<double, double>();
        
        public static void Initialize(int ones, int zeros)
        {
            if (ones == 0 || zeros == 0)
                throw new ArgumentException("Missing zeros or ones");
            optHGT = 0.05;
            if (Ones == ones && Zeros == zeros && HGTmat != null)
            {
                return; //this was pre-initialized sometime
            }

            Ones = ones;
            Zeros = zeros;
            var N = zeros + ones;
            HGTmat = new double[zeros + 1, ones + 1];
            HGTmat[0, 0] = 1.0; //Base condition

            //init zeros
            for (var i = 1; i < zeros + 1; i++)
                HGTmat[i, 0] = HGfromPrevious(HGTmat[i - 1, 0], N, ones, i - 1, 0, false);
            
            //init ones
            for (var j = 1; j < ones + 1; j++)
                HGTmat[0, j] = HGfromPrevious(HGTmat[0, j - 1], N, ones, j - 1, j - 1, true);
            
            //compute HG w/ dynamic program
            for (var i = 1; i < zeros + 1; i++)
            {
                HGTmat[i, 1] = HGfromPrevious(HGTmat[i - 1, 1], N, ones, i, 1, false);
                for (var j = 2; j < ones + 1; j++)
                {
                    HGTmat[i, j] = HGfromPrevious(HGTmat[i, j - 1], N, ones, i + j - 1, j - 1, true);
                }
            }

            var scoreToPval = new HashSet<double>();
            //Sum for the 'upper' HGT
            for (var diagsum = 0; diagsum <= zeros+ones; diagsum++)
            {
                var runsum = 0.0;
                for (var numones = Math.Min(diagsum,ones); numones >= 0; numones--)
                {
                    var numzeros = diagsum - numones;
                    if (numzeros > zeros)
                        continue;
                    runsum += HGTmat[numzeros, numones];
                    HGTmat[numzeros, numones] = runsum;
                    //if(runsum<0.5)
                    scoreToPval.Add(runsum);
                }
            }
            TotalPaths = Math.Min(Double.MaxValue, pathCounting(-0.1, out var pMat)); //Todo: should be N choose B
            var tmp=Accord.Math.Special.Binomial(N, ones);
            if (scoreToPval.Count < 100000)
            {
                Console.WriteLine("Mapping {0} mHG scores to pvalue.", scoreToPval.Count);
                int numMapped = 0;
                Parallel.ForEach(scoreToPval, score =>
                {
                    var pval = 1.0 - (pathCounting(score, out var pMat1) / TotalPaths);
                    ScoreMap.AddOrUpdate(score, pval, (a, b) => pval);
                    var iter = Interlocked.Increment(ref numMapped);
                    if (iter % 1000==0)
                        Console.Write("\r\r\r\r\r" + iter);
                });
                Console.WriteLine();
            }
            else
                Console.WriteLine("mHG Caching skipped, too many values.");
            Console.WriteLine("Done initializing HGT matrix of size {0}x{1}", zeros, ones);
        }

        /// <summary>
        /// Computes the recurrence relation for the hypergeometric pdf.
        /// </summary>
        /// <param name="hgt0">previous hg pdf </param>
        /// <param name="N">#items</param>
        /// <param name="K">Total possible successes</param>
        /// <param name="n0">previous num trials</param>
        /// <param name="k0">previous #success</param>
        /// <param name="isSuccess">Is the current trial a succcess</param>
        /// <returns></returns>
        public static double HGfromPrevious(double hgt0, int N, int K, int n0, int k0, bool isSuccess)
        {
            var hgt1 = hgt0 * ((n0 + 1) / (double) (N - n0)) *
                       (isSuccess ? (K - k0) / (double) (k0 + 1) : (N - K - n0 + k0) / (double) (n0 - k0 + 1));
            return hgt1;
        }



        /// <summary>
        /// Guarantees *at least* a pValThresh mHG significance boolean vector
        /// </summary>
        /// <param name="pValThresh"></param>
        /// <returns></returns>
        public static bool[] SampleSignificantEnrichmentVector(double pValThresh = 0.05)
        {
            pValThresh = Math.Max(pValThresh, ScoreMap.Min(v => v.Value));
            var outvec = new List<bool>();
            pathCounting(-1, out var pathCountMat);
            var pathList = new List<Tuple<int, int, double, double>>();
            for (var i = 0; i < Zeros + 1; i++)
            for (var j = 0; j < Ones + 1; j++)
                if(ScoreMap[HGTmat[i, j]] <= pValThresh)
                    pathList.Add(new Tuple<int, int, double, double>(i,j, pathCountMat[i,j], ScoreMap[HGTmat[i, j]]));

            var pathCountSum = pathList.Sum(v => Math.Log10(v.Item3));
            var rnd = new Random();
            var selection = rnd.NextDouble() * pathCountSum;
            double pathCountCumsum = 0.0;
            var item = pathList.OrderByDescending(v => v.Item3).SkipWhile(v => (pathCountCumsum += Math.Log10(v.Item3)) < selection).First();

            for (var k = 0; k < item.Item1; k++) //zerosInThresh
                outvec.Add(false);
            for (var k = 0; k < item.Item2; k++) //onesInThresh
                outvec.Add(true);
            outvec = outvec.OrderBy(v => StaticConfigParams.rnd.Next()).ToList(); 

            //Select remaining uniformly in vector
            var remainingOnes = Ones - item.Item2;
            var remainingZeros = Zeros - item.Item1;
            for (var k = 0; k < (Ones + Zeros - item.Item1 - item.Item2); k++)
            {
                var nextBool = StaticConfigParams.rnd.NextDouble() < (remainingOnes / remainingZeros) ? true : false;
                switch (nextBool)
                {
                    case true:
                        remainingOnes--;
                        break;
                    case false:
                        remainingZeros--;
                        break;
                }
                outvec.Add(nextBool);
            }

            return outvec.ToArray();
        }



        /// <summary>
        /// Counts with dynamic program the number of paths that dont go through a HGT score.
        /// </summary>
        /// <param name="hgtScore"></param>
        /// <returns></returns>
        public static double pathCounting(double hgtScore, out double[,] pMat)
        {
            pMat = new double[Zeros + 1, Ones + 1];
            //There is exactly one path that travel on edges
            for (var i = 0; i < Zeros + 1; i++)
                pMat[i, 0] = HGTmat[i, 0] > hgtScore ? 1 : 0;
            for (var j = 0; j < Ones + 1; j++)
                pMat[0, j] = HGTmat[0, j] > hgtScore ? 1 : 0;

            for (var i = 1; i < Zeros + 1; i++)
                for (var j = 1; j < Ones + 1; j++)
                {
                    var isInR = HGTmat[i, j] <= hgtScore;// + StaticConfigParams.TOLERANCE;
                    pMat[i, j] = isInR ? 0 : pMat[i - 1, j] + pMat[i, j - 1];
                }
            return pMat[Zeros,Ones];
        }


        public static Tuple<double, int, int[]> minimumHypergeometric(IEnumerable<bool> binVec, int tN = -1, int tB = -1, mHGCorrectionType correctMultiHypothesis = mHGCorrectionType.Exact, bool abortIfSubOpt = false)
        {
            var N = tN > 0 ? tN : Ones+Zeros;
            var B = tB > 0 ? tB : Zeros;
            var mHGT = 1.1;
            var currIndex = 0;
            var k = 0;
            bool newOpt = false;
            //OptDistVec is a vector that counts for each '1' in the binary vector the minimum number of 1's needed directly after it for a significant p-value
            var OptDistVec = new int[Ones+1];
            for (var i = 0; i < Ones + 1; i++)
                OptDistVec[i] = Ones; // default max step size (int.maxvalue)
            for (var i = 0; i < Ones + 1; i++) if (HGTmat[1, i] <= optHGT.Value) OptDistVec[0] = Math.Min(OptDistVec[0], i);
            var n = 0;
            foreach (var el in binVec)
            {
                if (el)
                {
                    k++;
                    var currHGT = HGTmat[n - k + 1, k]; //[num zeros, num ones]
                    //currHGT = ScoreMap[currHG];
                    if (currHGT < mHGT)
                    {
                        currIndex = n;
                        mHGT = currHGT;
                    }
                    //check distance to optimum
                    if (!newOpt && abortIfSubOpt && HGTmat[n - k + 1, Ones] > optHGT)
                        return new Tuple<double, int, int[]>(1.0, -1, new int[0]);
                    if (optHGT.HasValue && optHGT <= currHGT)
                    {
                        for (var i = k; i < Ones+1; i++)
                            if (HGTmat[n - k + 1, i] <= optHGT.Value)
                                OptDistVec[k] = Math.Min(OptDistVec[k], i - k);
                    }
                    else
                    {
                        newOpt = true;
                        lock (concLocker)
                        {
                            optHGT = currHGT;
                        }
                    }
                }
                n++;
            }
            //for (var i = 0; i < Ones; i++) if(OptDistVec[i] > Ones) OptDistVec[i] = 1; //this happens when we cannot fulfil the required number of ones at this threshold.
            double pval = -1;
            switch (correctMultiHypothesis)
            {
                case mHGCorrectionType.Exact:
                    pval = ScoreMap.GetOrAdd(mHGT, 
                        1.0 - (pathCounting(mHGT, out var pMat1) / TotalPaths));
                    break;
                case mHGCorrectionType.None:
                    pval = mHGT;
                    break;
                case mHGCorrectionType.Bonferroni:
                    pval = mHGT * N;
                    break;
                case mHGCorrectionType.Lipson:
                    pval = mHGT * B;
                    break;
            }
            //if (newOpt) Console.WriteLine("new mHG OPT={0}", pval);
            return new Tuple<double, int, int[]>(pval, currIndex + 1, OptDistVec);
        }

        #region mHG privates
        private static double HGT(double currHG, int n, int N, int B, int k)
        {

            if (Math.Abs(currHG) < 10*double.Epsilon)
            {
                double enrichment1 = k / (double)n;
                double enrichment2 = B / (double)N;
                if (enrichment1 < enrichment2)
                {
                    return 1;
                }
            }

            int minNb = Math.Min(n, B);
            double tail = currHG;
            int tmp = N - n - B + 1;

            for (int i = k; i < minNb; i++)
            {
                currHG = currHG * ((n - i) * (B - i)) / ((i + 1) * (tmp + i));
                tail += currHG;
            }
            return tail;
        }

        //B=|falses|
        private static double mHGpValue(int N, int B, double mHGT)
        {
            if (mHGT >= 1)
                return 1;
            if (B == 0 || B >= N)
                return 0;

            var W = N - B;
            const int R_ZONE = 0;
            var baseHG = 1.0;
            var mat = new double[W + 1][];
            for (var i = 0; i < W + 1; i++)
                mat[i] = new double[N + 1];
            mat[0][0] = 1;

            for (var n = 1; n <= N; n++)
            {
                int min_nW;
                if (W >= n)
                {
                    min_nW = n;
                    baseHG *= (double)(W - n + 1) / (N - n + 1);
                }
                else
                {
                    min_nW = W;
                    baseHG *= (double)n / (n - W);
                }
                var tailHG = baseHG;
                var currHG = baseHG;

                int k = min_nW;
                for (; tailHG <= mHGT && k > 0; k--)
                {
                    // k is the number of ones in current vector
                    currHG *= (double)(k * (N - W - n + k)) / ((n - k + 1) * (W - k + 1));
                    tailHG += currHG;
                    mat[k][n] = R_ZONE;
                }

                // second loop, starts when k is the maximal for which
                // HGT(N,K,n,k)> mHGT
                for (; k > 0; k--)
                {
                    // calculate current cell value by two optional cells from
                    // which it can be reached
                    // 1. last element in vector is 0

                    mat[k][n] = 0; //////////////////////// for printing reasons
                    if (mat[k][n - 1] <= 1) //////////////////////// for printing reasons
                        mat[k][n] += mat[k][n - 1] * (N - W - n + k + 1) / (N - n + 1);
                    // 2. last element in vector is 1
                    if (mat[k - 1][n - 1] <= 1) //////////////////////// for printing reasons
                        mat[k][n] += mat[k - 1][n - 1] * (W - k + 1) / (N - n + 1);
                }
                mat[k][n] = 0; //////////////////////// for printing reasons
                mat[0][n] += mat[0][n - 1] * (N - W - n + 1) / (N - n + 1);
            }
            double result = 0;
            for (var i = 0; i <= W; i++)
            {
                result += mat[i][W];
            }
            return 1 - result;

        }


        /// <summary>
        /// The calculate p value.
        /// </summary>
        /// <param name="totalElements">
        /// The totalElements.
        /// </param>
        /// <param name="totalMatches">
        /// The totalMatches.
        /// </param>
        /// <param name="mHGT">
        /// The mhgt.
        /// </param>
        /// <param name="threshold">
        /// The threshold.
        /// </param>
        /// <returns>
        /// The <see cref="double"/>.
        /// </returns>
        public static double CalculatePValue(int totalElements, int totalMatches, double mHGT, int threshold, out double[,] mat)
        {
            // ReSharper restore InconsistentNaming
            int minNthreshold = totalElements < threshold ? totalElements : threshold;
            int minBNthresh = totalMatches < threshold ? totalMatches : threshold;
            mat = new double[minBNthresh + 1, minNthreshold + 1];
            if (totalMatches == 0)
            {
                return 1;
            }
            
            for (int i = 0; i <= minBNthresh; i++)
            {
                for (int j = 0; j <= minNthreshold; j++)
                {
                    mat[i, j] = 0;
                }
            }

            mat[0, 0] = 1;
            double baseHG = 1; // holds HG(m_totalElements,K,m_elementsOnTop,min(m_elementsOnTop,K))

            for (int n = 1; n <= minNthreshold; n++)
            {
                // m_elementsOnTop is the number of elemnets in current vector
                int minNb;
                if (totalMatches >= n)
                {
                    minNb = n;
                    baseHG = baseHG * (totalMatches - n + 1) / (totalElements - n + 1);
                }
                else
                {
                    minNb = totalMatches;
                    baseHG = baseHG * n / (n - totalMatches);
                }

                if (baseHG <= double.MinValue)
                {
                    baseHG = double.MinValue;
                    minNthreshold = n;
                }

                double tailHG = baseHG;
                double currHG = baseHG;

                // first loop - sum up the tail, until the sum is bigger than mHGT
                int b;
                for (b = minNb; tailHG <= mHGT && b > 0; b--)
                {
                    // matchesOnTop is the number of ones in current vector
                    currHG = currHG * (b * (totalElements - totalMatches - n + b)) / ((n - b + 1) * (totalMatches - b + 1));

                    // if (currHG == 0) currHG = Double.MIN_VALUE;///
                    tailHG += currHG;
                    mat[b, n] = c_rZone;
                }

                // second loop, starts when matchesOnTop is the maximal for which
                // HGT(m_totalElements,m_totalSuccesses,m_elementsOnTop,matchesOnTop)> mHGT
                for (; b > 0; b--)
                {
                    // calculate current cell value by two optional cells from
                    // which it can be reached
                    // 1. last element in vector is 0
                    mat[b, n] = 0; //////////////////////// for printing reasons
                    if (mat[b, n - 1] <= 1)
                    {
                        //////////////////////// for printing reasons
                        mat[b, n] += mat[b, n - 1] * (totalElements - totalMatches - n + b + 1) / (totalElements - n + 1);
                    }

                    // 2. last element in vector is 1
                    if (mat[b - 1, n - 1] <= 1)
                    {
                        //////////////////////// for printing reasons
                        mat[b, n] += mat[b - 1, n - 1] * (totalMatches - b + 1) / (totalElements - n + 1);
                    }

                    // if (mat[matchesOnTop, m_elementsOnTop] == 0) mat[matchesOnTop, m_elementsOnTop] = Double.MIN_VALUE;///
                }

                mat[0, n] = mat[0, n - 1] * (totalElements - totalMatches - n + 1) / (totalElements - n + 1);

                if (Math.Abs(mat[0, n] - double.MinValue) < Epsilon)
                {
                    minNthreshold = n;

                    // System.err.println("2: m_elementsOnTop = "+m_elementsOnTop);
                }
            }

            double result = 0;
            for (int i = 0; i <= minBNthresh; i++)
            {
                result += mat[i, minNthreshold];
            }

            return 1 - result;
        }

        #endregion
    }

    public static class MathExtensions
    {
        public static double NChooose3(int N)
        {
            return (1.0 / 6.0) * (N - 2) * (N - 1) * N;
        }

        public static double NChooose2(int N)
        {
            return (1.0 / 2.0) * (N - 1) * N;
        }

        public static double Binomial(int N, int k)
        {
            return Stieltjes3Factorial(N) / (Stieltjes3Factorial(k) * Stieltjes3Factorial(N - k));
        }

        /// <summary>
        /// Implements Stieltjes's factorial approximation.
        /// Based on http://www.luschny.de/math/factorial/approx/SimpleCases.html
        /// </summary>
        /// <param name="x"></param>
        /// <returns></returns>
        public static double Stieltjes3Factorial(int x)
        {
            var y = x + 1.0;
            var p = 1.0;

            while (y < 7)
            {
                p = p * y;
                y = y + 1.0;
            }
            var r = Math.Exp(y * Math.Log(y) - y + 1.0 / (12.0 * y + 2.0 / (5.0 * y + 53.0 / (42.0 * y))));
            if (x < 7)
                r = r / p;
            var roundToNearest = Math.Round(r * Math.Sqrt(2 * Math.PI / y));
            return (long)roundToNearest;

        }

        public static void Print<T>(this T[,] matrix, bool transpose = false)
        {
            var nRows = transpose ? matrix.GetLength(1) - 1 : matrix.GetLength(0) - 1;
            var nCols = transpose ? matrix.GetLength(0) - 1 : matrix.GetLength(1) - 1;
            for (int i = nRows; i >= 0 ; i--)
            {
                for (int j = 0; j < nCols; j++)
                {
                    Console.Write("{0:##.###}\t", Convert.ToDouble(transpose ? matrix[j, i] : matrix[i, j]));
                }
                Console.WriteLine();
            }
        }
    }
}
