using System;
using System.Collections.Generic;
using System.Text;

namespace SpatialEnrichment
{
    public class ResponseClass
    {
        public SpatialmHGSpot[] SpatialmHGSpots { get; set; }
        public ResponseClass(SpatialmHGSpot[] i_SpatialmHGSpots)
        {
            SpatialmHGSpots = i_SpatialmHGSpots;
        }

        public override string ToString()
        {
            string returnStr = "";

            for (int i = 0; i < SpatialmHGSpots.Length; i++)
            {
                returnStr += SpatialmHGSpots[i].ToString() + " ";
            }

            return returnStr;
        }
    }
}
