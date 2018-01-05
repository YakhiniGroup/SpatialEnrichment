using System;
using System.Collections.Generic;
using System.Text;

namespace SpatialEnrichment
{
    public class Spot
    {
        public double Lon { get; set; }
        public double Lat { get; set; }
        public string Name { get; set; }
        public bool Show { get; set; }
        public double Info { get; set; }

        public override string ToString()
        {
            return "[Lon = " + Lon + ", Name = " + Name + ", Show = " + Show + ", Lat = " + Lat + ", Info = " + Info + "]";
        }
    }
}
