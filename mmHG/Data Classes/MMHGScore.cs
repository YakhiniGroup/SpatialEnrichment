using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace mmHG.DataClasses
{
    public class MMHGScore : Score
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
        public MMHGScore(int totalElements, int totalMatches, int elementsOnTop, int matchesOnTop, double score, int n1, int n2, int commonElements) :
            base(totalElements, totalMatches, elementsOnTop, matchesOnTop, score)
        {
            N1 = n1;
            N2 = n2;
            CommonElements = commonElements;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="HGScore"/> class.
        /// </summary>
        /// <param name="score">
        /// The score.
        /// </param>
        public MMHGScore(Score score, int n1, int n2, int commonElements) :
            base(score) 
        {
            N1 = n1;
            N2 = n2;
            CommonElements = commonElements;
        }

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
            double pValueBoundary = ScoreValue * m_totalElements * m_totalElements;

            m_pValue = Math.Min(pValueBoundary, 1);
        }

        public int N1 { get; private set; }
        public int N2 { get; private set; }
        public int CommonElements { get; private set; }
    }
}
