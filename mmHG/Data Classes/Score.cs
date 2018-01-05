using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace mmHG.DataClasses
{
    public abstract class Score
    {
        /// <summary>
        /// The epsilon.
        /// </summary>
        public static readonly double Epsilon = 0.01;

        /// <summary>
        /// The m_total elements.
        /// </summary>
        protected readonly int m_totalElements;

        /// <summary>
        /// The m_total matches.
        /// </summary>
        protected readonly int m_totalMatches;

        /// <summary>
        /// The m_elements on top.
        /// </summary>
        protected readonly int m_elementsOnTop;

        /// <summary>
        /// The m_matches on top.
        /// </summary>
        protected readonly int m_matchesOnTop;

        /// <summary>
        /// The p value.
        /// </summary>
        protected double? m_pValue;

        /// <summary>
        /// Initializes a new instance of the <see cref="Score"/> class.
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
        public Score(int totalElements, int totalMatches, int elementsOnTop, int matchesOnTop, double score)
        {
            m_totalElements = totalElements;
            m_elementsOnTop = elementsOnTop;
            m_totalMatches = totalMatches;
            m_matchesOnTop = matchesOnTop;
            ScoreValue = score;
            m_pValue = null;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Score"/> class.
        /// </summary>
        /// <param name="score">
        /// The score.
        /// </param>
        public Score(Score score)
        {
            m_totalElements = score.m_totalElements;
            m_elementsOnTop = score.m_elementsOnTop;
            m_totalMatches = score.m_totalMatches;
            m_matchesOnTop = score.m_matchesOnTop;
            ScoreValue = score.ScoreValue;
            m_pValue = null;
        }

        /// <summary>
        /// Gets or sets the score.
        /// </summary>
        public double ScoreValue 
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the p value.
        /// </summary>
        public double PValue
        {
            get
            {
                if(!m_pValue.HasValue)
                {
                    CalcPValue();
                }

                return m_pValue.Value;
            }
        }

        /// <summary>
        /// The to string.
        /// </summary>
        /// <returns>
        /// The <see cref="string"/>.
        /// </returns>
        public override string ToString()
        {
            string rep = "m_totalElements " + m_totalElements + ", m_totalMatches " + m_totalMatches + ", m_elementsOnTop " + m_elementsOnTop + ", m_matchesOnTop " + m_matchesOnTop + ", score " + ScoreValue;
            if (m_pValue < 1)
            {
                rep += ", p-value " + m_pValue;
            }

            return rep;
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
        protected abstract void CalcPValue();
    }
}
