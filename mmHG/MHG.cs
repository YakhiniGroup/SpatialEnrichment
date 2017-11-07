// --------------------------------------------------------------------------------------------------------------------
// <copyright file="MHG.cs" company="Dalia">
//   2014
// </copyright>
// <summary>
//   Defines the MHG type.
// </summary>
// --------------------------------------------------------------------------------------------------------------------
namespace mmHG
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    using mmHG.DataClasses;

    /// <summary>
    /// The MHG.
    /// </summary>
    public static class MHG
    {
        static public void SetMaxN(int maxN)
        {
            logFactorialUpper = new double[maxN];
            logFactorialLower = new double[maxN];

            for (int i = 1; i <= Math.Min(20, maxN - 1); ++i)
            {
                logFactorialUpper[i] = logFactorialUpper[i-1] + Math.Log(i); 
                logFactorialLower[i] = logFactorialUpper[i];
            }

            for (int i = 21; i < maxN; ++i)
            {
                logFactorialUpper[i] = LogStirlingApproximation(i, true);
                logFactorialLower[i] = LogStirlingApproximation(i, false);
            }
        }

        #region Constants and Fields

        /// <summary>
        /// The c_r zone.
        /// </summary>
        private const int c_rZone = 0;

        /// <summary>
        /// The c_threshold.
        /// </summary>
        private const int c_threshold = 10000; // The maximal position in the motif binary vector we calculate the p-value for

        private static double[] logFactorialUpper;
        private static double[] logFactorialLower;

        #endregion

        #region Public Methods

        /// <summary>
        /// The bound minimal hg.
        /// </summary>
        /// <param name="totalElements">
        /// The total elements.
        /// </param>
        /// <param name="indices">
        /// The indices.
        /// </param>
        /// <returns>
        /// The <see cref="double"/>.
        /// </returns>
        public static double BoundMinimalHG(int totalElements, SortedSet<int> indices)
        {
            double min = 1;
            var truncated = new SortedSet<int>();
            foreach (int index in indices)
            {
                truncated.Add(index);
                int totalMatches = truncated.Count;
                double hg = HG(totalElements, index, totalMatches, totalMatches, true);
                if (hg < min)
                {
                    min = hg;
                }
            }

            return min;
        }

        /// <summary>
        /// The calculate hgt.
        /// </summary>
        /// <param name="totalElements">
        /// The total elements.
        /// </param>
        /// <param name="indices">
        /// The indices.
        /// </param>
        /// <param name="elementsOnTop">
        /// The elements on top.
        /// </param>
        /// <returns>
        /// The <see cref="HGScore"/>.
        /// </returns>
        public static HGScore CalculateHGT(int totalElements, SortedSet<int> indices, int elementsOnTop)
        {
            int totalMatches = indices.Count;
            int matchesOnTop = 0;

            foreach (int index in indices)
            {
                if (index <= elementsOnTop)
                {
                    matchesOnTop++;
                }
                else
                {
                    break;
                }
            }

            HGScore s = CalculateHGT(totalElements, elementsOnTop, totalMatches, matchesOnTop);
            return s;
        }

        /// <summary>
        /// The calculate hgt.
        /// </summary>
        /// <param name="totalElements">
        /// The total elements.
        /// </param>
        /// <param name="elementsOnTop">
        /// The elements on top.
        /// </param>
        /// <param name="totalMatches">
        /// The total matches.
        /// </param>
        /// <param name="matchesOnTop">
        /// The matches on top.
        /// </param>
        /// <returns>
        /// The <see cref="HGScore"/>.
        /// </returns>
        public static HGScore CalculateHGT(int totalElements, int elementsOnTop, int totalMatches, int matchesOnTop)
        {
#if DEBUG
            if (totalMatches > totalElements || elementsOnTop > totalElements || matchesOnTop > totalMatches || matchesOnTop > elementsOnTop)
            {
                throw new ArgumentException(
                    "Wrong Parameter Sizes!: m_totalElements=" + totalElements + " m_elementsOnTop=" + elementsOnTop + " m_totalSuccesses=" + totalMatches
                    + " matchesOnTop=" + matchesOnTop);
            }
#endif
            double hg = HG(totalElements, elementsOnTop, totalMatches, matchesOnTop, true);
            double hgt = HGT(hg, elementsOnTop, totalElements, totalMatches, matchesOnTop);
            return new HGScore(totalElements, totalMatches, elementsOnTop, matchesOnTop, hgt);
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
        public static double CalculatePValue(int totalElements, int totalMatches, double mHGT, int threshold = c_threshold)
        {
            // ReSharper restore InconsistentNaming
            int minNthreshold = totalElements < threshold ? totalElements : threshold;
            int minBNthresh = totalMatches < threshold ? totalMatches : threshold;
            if (totalMatches == 0)
            {
                return 1;
            }

            var mat = new double[minBNthresh + 1, minNthreshold + 1];
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

                if (Math.Abs(mat[0, n] - double.MinValue) < HGScore.Epsilon)
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

        /// <summary>
        /// The check vector.
        /// </summary>
        /// <param name="totalElements">
        /// The total elements.
        /// </param>
        /// <param name="indices">
        /// The indices.
        /// </param>
        /// <param name="threshold">
        /// The threshold.
        /// </param>
        /// <returns>
        /// The <see cref="bool"/>.
        /// </returns>
        public static bool CheckVector(int totalElements, SortedSet<int> indices, double threshold)
        {
            int totalMatches = indices.Count;
            double ratio = totalMatches / (double)totalElements;
            int matchesOnTop = 1;

            foreach (int index in indices)
            {
                int elementsOnTop = index;
                if (matchesOnTop / (double)elementsOnTop > ratio)
                {
                    double hg = HG(totalElements, elementsOnTop, totalMatches, matchesOnTop, true);
                    if (hg < threshold)
                    {
                        double hgt = HGT(hg, elementsOnTop, totalElements, totalMatches, matchesOnTop);
                        if (hgt <= threshold)
                        {
                            return true;
                        }
                    }
                }

                matchesOnTop++;
            }

            return false;
        }

        /// <summary>
        /// The hg.
        /// </summary>
        /// <param name="totalElements">
        /// The totalElements.
        /// </param>
        /// <param name="elementsOnTop">
        /// The elementsOnTop.
        /// </param>
        /// <param name="totalMatches">
        /// The totalMatches.
        /// </param>
        /// <param name="matchesOnTop">
        /// The matchesOnTop.
        /// </param>
        /// <param name="mode">
        /// The mode.
        /// </param>
        /// <returns>
        /// The <see cref="double"/>.
        /// </returns>
        public static double HG(int totalElements, int elementsOnTop, int totalMatches, int matchesOnTop, bool isUpper)
        {
            double log = LogNCk(elementsOnTop, matchesOnTop, isUpper) + LogNCk(totalElements - elementsOnTop, totalMatches - matchesOnTop, isUpper) - LogNCk(totalElements, totalMatches, !isUpper);
            return Math.Exp(log);
        }

        /// <summary>
        /// Based on Eran's code. Computes the hyper geometric tail.
        /// </summary>
        /// <param name="currHG">
        /// The current HG(m_totalElements,K,m_elementsOnTop,k) score.
        /// </param>
        /// <param name="elementsOnTop">
        /// The number of elements so far
        /// </param>
        /// <param name="totalElements">
        /// The number of elements in vector
        /// </param>
        /// <param name="totalMatches">
        /// The number of 1's in vector
        /// </param>
        /// <param name="matchesOnTop">
        /// The number of 1's in the small vector that goes until m_elementsOnTop.
        /// </param>
        /// <returns>
        /// The HG tail score <see cref="double"/>.
        /// </returns>
        public static double HGT(double currHG, int elementsOnTop, int totalElements, int totalMatches, int matchesOnTop)
        {
            // ReSharper restore InconsistentNaming
            if (Math.Abs(currHG) < HGScore.Epsilon)
            {
                double enrichment1 = matchesOnTop / (double)elementsOnTop;
                double enrichment2 = totalMatches / (double)totalElements;
                if (enrichment1 < enrichment2)
                {
                    return 1;
                }
            }

            int minNb = Math.Min(elementsOnTop, totalMatches);
            double tail = currHG;
            int tmp = totalElements - elementsOnTop - totalMatches + 1;

            for (int i = matchesOnTop; i < minNb; i++)
            {
                currHG = currHG * ((elementsOnTop - i) * (totalMatches - i)) / ((i + 1) * (tmp + i));
                tail += currHG;
            }

            return tail;
        }

        /// <summary>
        /// The log factorial.
        /// </summary>
        /// <param name="n">
        /// The elementsOnTop.
        /// </param>
        /// <param name="mode">
        /// The mode.
        /// </param>
        /// <returns>
        /// The <see cref="double"/>.
        /// </returns>
        public static double LogFactorial(int n, bool isUpper)
        {
            return isUpper ? logFactorialUpper[n] : logFactorialLower[n];
        }

        /// <summary>
        /// The log factorial exact.
        /// </summary>
        /// <param name="n">
        /// The elementsOnTop.
        /// </param>
        /// <returns>
        /// The <see cref="double"/>.
        /// </returns>
        public static double LogFactorialExact(int n)
        {
            if (n == 0)
            {
                return 0;
            }

            return Math.Log(n) + LogFactorialExact(n - 1);
        }

        /// <summary>
        /// The calculate minimal hg.
        /// </summary>
        /// <param name="totalElements">
        /// The elementsOnTop.
        /// </param>
        /// <param name="indices">
        /// The indices.
        /// </param>
        /// <param name="threshold">
        /// The threshold.
        /// </param>
        /// <returns>
        /// The <see cref="HGScore"/>.
        /// </returns>
        public static MHGScore CalculateMinimalHG(int totalElements, SortedSet<int> indices, double threshold)
        {
            int totalMatches = indices.Count;
            double ratio = totalMatches / (double)totalElements;
            var minHG = new MHGScore(totalElements, totalMatches, 0, 0, 1);
            int matchesOnTop = 1;

            foreach (int index in indices)
            {
                int elementsOnTop = index;
                if (matchesOnTop / (double)elementsOnTop > ratio)
                {
                    double hg = HG(totalElements, elementsOnTop, totalMatches, matchesOnTop, true);
                    if (hg <= threshold)
                    {
                        double hgt = HGT(hg, elementsOnTop, totalElements, totalMatches, matchesOnTop);
                        if (hgt <= threshold && hgt < minHG.ScoreValue)
                        {
                            minHG = new MHGScore(totalElements, totalMatches, elementsOnTop, matchesOnTop, hgt);
                        }
                    }
                }

                matchesOnTop++;
            }

            return minHG;
        }

        /// <summary>
        /// The fast score.
        /// </summary>
        /// <param name="totalElements">
        /// The total elements.
        /// </param>
        /// <param name="indices">
        /// The indices.
        /// </param>
        /// <param name="i">
        /// The i.
        /// </param>
        /// <returns>
        /// The <see cref="HGScore"/>.
        /// </returns>
        public static MHGScore FastScore(int totalElements, SortedSet<int> indices, int i)
        {
            Score min = new MHGScore(totalElements, 0, 0, 0, 1);
            int totalMatches = indices.Count;
            for (double j = Math.Pow(2, i); j >= 2; j /= 2)
            {
                var elementsOnTop = (int)(totalElements / j);
                int matchesOnTop = indices.Take(elementsOnTop + 1).Count();
                Score hGScore = CalculateHGT(totalElements, elementsOnTop, totalMatches, matchesOnTop);
                if (hGScore.ScoreValue < min.ScoreValue)
                {
                    min = hGScore;
                }
            }

            return new MHGScore(min);
        }

        /// <summary>
        /// The log Stirling approximation.
        /// </summary>
        /// <param name="n">
        /// The elementsOnTop.
        /// </param>
        /// <param name="mode">
        /// The mode.
        /// </param>
        /// <returns>
        /// The <see cref="double"/>.
        /// </returns>
        public static double LogStirlingApproximation(int n, bool isUpper)
        {
            double addition = isUpper ? (1.0 / (12 * n)) : (1.0 / ((12 * n) + 1));
            return (0.5 * Math.Log(2 * Math.PI * n)) + (n * Math.Log(n / Math.E)) + addition;
        }

        /// <summary>
        /// The log_n c k.
        /// </summary>
        /// <param name="n">
        /// The elementsOnTop.
        /// </param>
        /// <param name="k">
        /// The k.
        /// </param>
        /// <param name="mode">
        /// The mode.
        /// </param>
        /// <returns>
        /// The <see cref="double"/>.
        /// </returns>
        public static double LogNCk(int n, int k, bool isUpper)
        {
            return LogFactorial(n, isUpper) - LogFactorial(k, !isUpper) - LogFactorial(n - k, !isUpper);
        }

        #endregion
    }
}
