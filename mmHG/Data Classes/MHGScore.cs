using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace mmHG.DataClasses
{
    public class MHGScore : Score
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
        public MHGScore(int totalElements, int totalMatches, int elementsOnTop, int matchesOnTop, double score) : 
            base(totalElements, totalMatches, elementsOnTop, matchesOnTop, score) {}

        /// <summary>
        /// Initializes a new instance of the <see cref="HGScore"/> class.
        /// </summary>
        /// <param name="score">
        /// The score.
        /// </param>
        public MHGScore(Score score) :
            base(score) {}

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
            double pValue = MHG.CalculatePValue(m_totalElements, m_totalMatches, ScoreValue);
            if (pValue < ScoreValue || pValue > ScoreValue * m_totalMatches)
            {
                pValue = ScoreValue * m_totalMatches;
            }

            m_pValue = Math.Min(pValue, 1);
        }
    }
}
