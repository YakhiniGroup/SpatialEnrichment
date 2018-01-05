// --------------------------------------------------------------------------------------------------------------------
// <copyright file="MMHG.cs" company="Dalia">
//   2014
// </copyright>
// <summary>
//   The mmhg.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace mmHG
{
    using System;
    using System.Collections.Generic;
    using mmHG.DataClasses;

    /// <summary>
    /// The mmhg.
    /// </summary>
    public class MMHG
    {
        #region Constants and Fields

        /// <summary>
        ///     The c_max threshold.
        /// </summary>
        private const int c_maxThreshold = 3000;

        #endregion

        #region Public Methods

        /// <summary>
        /// This method takes as input a permutation (and possibly a cutoff) and returns the mmHG score and the mmHG p-value.
        /// </summary>
        /// <param name="perm">
        /// A permutation over the numbers 1,...,totalElements
        /// </param>
        /// <param name="nstar">
        /// Indicates whether the list should be cut at a specific position.
        /// </param>
        /// <returns>
        /// The mmHG score which is the minimal hyper-geometric tail found. The p-value field contains the Bonferroni correction.
        ///     <see cref="HGScore"/>
        ///     .
        /// </returns>
        public static MMHGScore CalcScore(int[] perm)
        {
            int totalElements = perm.Length;
            var indMapper = new Dictionary<int, int>();
            int totalElementsBeforeThreshold =  Math.Min(totalElements, c_maxThreshold);

            Score min = new HGScore(totalElements, 0, 0, 0, 1);

            if(perm == null || perm.Length == 0)
            {
                return new MMHGScore(min,-1,-1,-1);
            }

            for (int k = 0; k < totalElements; k++)
            {
                indMapper.Add(perm[k], k);
            }

            int finalIndex1 = perm.Length - 1;
            int finalIndex2 = perm.Length - 1;
            int commonElements = perm.Length;

            int maxVal = perm[0];
            for (int n2 = 1; n2 < totalElementsBeforeThreshold; n2++)
            {
                // n2 represents the position in the permutation
                if (perm[n2] > maxVal)
                {
                    maxVal = perm[n2];
                }

                int b = 0;
                for (int n1 = 1; n1 <= maxVal && n1 <= totalElementsBeforeThreshold; n1++)
                {
                    // n1 represents the range 0...n1-1
                    if (indMapper[n1 - 1] < n2)
                    {
                        b++;
                        Score s = MHG.CalculateHGT(totalElements, n1, n2, b);
                        if (s.ScoreValue < min.ScoreValue)
                        {
                            min = s;
                            finalIndex1 = n1;
                            finalIndex2 = n2;
                            commonElements = b;
                        }
                    }
                } 
            }

            return new MMHGScore(min, finalIndex1, finalIndex2, commonElements);
        }

        #endregion
    }
}
