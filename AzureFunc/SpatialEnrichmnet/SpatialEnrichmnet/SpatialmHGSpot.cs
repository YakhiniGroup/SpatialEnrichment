using System;
using System.Collections.Generic;
using System.Text;

namespace SpatialEnrichment
{
    public class SpatialmHGSpot
    {
        public double Lon { get; set; } //X
        public double Lat { get; set; } //Y
        public int MHGthreshold { get; set; }
        public double Pvalue { get; set; }

        public SpatialmHGSpot(double i_Lon, double i_Lat, int i_MHGthreshold, double i_Pvalue)
        {
            Lon = i_Lon;
            Lat = i_Lat;
            MHGthreshold = i_MHGthreshold;
            Pvalue = i_Pvalue;
        }

        public override string ToString()
        {
            return "[Lon = " + Lon + ", Lat = " + Lat + ", MHGthreshold = " + MHGthreshold + ", Pvalue = " + Pvalue + "]";
        }
    }
}
