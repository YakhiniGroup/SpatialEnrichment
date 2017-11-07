// --------------------------------------------------------------------------------------------------------------------
// <copyright file="HGScore.cs" company="Dalia">
//   2014
// </copyright>
// <summary>
//   Defines the HGScore type.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace mmHG.DataClasses
{
    using System;
    using System.Diagnostics.CodeAnalysis;

    /// <summary>
    /// The MMHG score.
    /// </summary>
    public class HGScore : Score
    {
                // <summary>
        /// Initializes a new instance of the <see cref="HGScore"/> class.
        /// </summary>
        /// <param name="totalElements">
        /// The m_totalElements.
        /// </param>
        /// <param name="totalMatches">
        /// The m_totalMatches.
        /// </param>
        /// <param name="elementsOnTop">
        /// The m_elementsOnTop.
        /// </param>
        /// <param name="matchesOnTop">
        /// The m_matchesOnTop.
        /// </param>
        /// <param name="score">
        /// The score.
        /// </param>
        public HGScore(int totalElements, int totalMatches, int elementsOnTop, int matchesOnTop, double score) : 
            base(totalElements, totalMatches, elementsOnTop, matchesOnTop, score) {}

        /// <summary>
        /// Calculates the p-value.
        /// </summary>
        /// <param name="threshold">
        /// The threshold.
        /// </param>
        /// <returns>
        /// The <see cref="double"/>.
        /// </returns>
        protected override void CalcPValue()
        {
            m_pValue = ScoreValue;
        }
    }
}