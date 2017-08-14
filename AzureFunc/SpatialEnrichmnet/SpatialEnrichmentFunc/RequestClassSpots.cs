using System;
using System.Collections.Generic;
using System.Text;

namespace SpatialEnrichment
{
    public class RequestClassSpots
    {
        public Spot[] Spots { get; set; }

        public override string ToString()
        {
            string returnStr = "";

            for (int i = 0; i < Spots.Length; i++)
            {
                returnStr += Spots[i].ToString() + " ";
            }

            return returnStr;
        }
    }
}





