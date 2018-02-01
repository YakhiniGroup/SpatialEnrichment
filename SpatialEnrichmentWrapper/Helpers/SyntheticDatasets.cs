using SpatialEnrichment;
using SpatialEnrichment.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SpatialEnrichmentWrapper.Helpers
{
    public static class SyntheticDatasets
    {
        public static List<Tuple<ICoordinate, bool>> SinglePlantedEnrichment(int dimensionality)
        {
            var rnd = new Random();
            List<ICoordinate> coordinates = new List<ICoordinate>();
            List<bool> labels = new List<bool>();
            ICoordinate pivot = dimensionality == 2 ? Coordinate.MakeRandom() : Coordinate3D.MakeRandom();
            var bonferroniFactor = dimensionality == 2 ? MathExtensions.NChooose2(mHGJumper.Lines + 1) : MathExtensions.NChooose3(mHGJumper.Lines + 1);
            for (var i = 0; i < (mHGJumper.Ones + mHGJumper.Zeros); i++)
            {
                switch (dimensionality)
                {
                    case 2:
                        coordinates.Add(Coordinate.MakeRandom());
                        break;
                    case 3:
                        coordinates.Add(Coordinate3D.MakeRandom());
                        break;
                }
            }
            coordinates = coordinates.OrderBy(t => t.EuclideanDistance(pivot)).ToList();
            labels = mHGJumper.SampleSignificantEnrichmentVector(0.05 / bonferroniFactor).ToList();
            mHGJumper.optHGT = 0.05;
            return new List<Tuple<ICoordinate, bool>>(coordinates.Zip(labels, (a, b) => new Tuple<ICoordinate, bool>(a, b)));
        }

    }
}
