using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SpatialEnrichment;
using SpatialEnrichment.Helpers;

namespace SpatialEnrichmentWrapper.Helpers
{
    public static class RandomInstance
    {
        public static Tuple<List<ICoordinate>, List<bool>> RandomizeCoordinatesAndSave(int numcoords, ConfigParams Config, bool save = true)
        {
            List<ICoordinate> coordinates = new List<ICoordinate>();
            List<bool> labels = new List<bool>();
            bool instance_created = false;
            while (!instance_created)
            {
                if ((Config.ActionList & Actions.Instance_Uniform) != 0)
                {
                    for (var i = 0; i < numcoords; i++)
                    {
                        if (StaticConfigParams.RandomInstanceType == typeof(Coordinate))
                            coordinates.Add(Coordinate.MakeRandom());
                        else if (StaticConfigParams.RandomInstanceType == typeof(Coordinate3D))
                            coordinates.Add(Coordinate3D.MakeRandom());
                        labels.Add(StaticConfigParams.rnd.NextDouble() > StaticConfigParams.CONST_NEGATIVELABELRATE);
                    }
                }
                if ((Config.ActionList & Actions.Instance_PlantedSingleEnrichment) != 0)
                {
                    for (var i = 0; i < numcoords; i++)
                        if (StaticConfigParams.RandomInstanceType == typeof(Coordinate))
                            coordinates.Add(Coordinate.MakeRandom());
                        else if (StaticConfigParams.RandomInstanceType == typeof(Coordinate3D))
                            coordinates.Add(Coordinate3D.MakeRandom());
                    ICoordinate pivotCoord = null;
                    if (StaticConfigParams.RandomInstanceType == typeof(Coordinate))
                        pivotCoord = Coordinate.MakeRandom();
                    else if (StaticConfigParams.RandomInstanceType == typeof(Coordinate3D))
                        pivotCoord = Coordinate3D.MakeRandom();

                    var prPos = (int)Math.Round((1.0 - StaticConfigParams.CONST_NEGATIVELABELRATE) * numcoords);
                    mHGJumper.Initialize(prPos, numcoords - prPos);
                    coordinates = coordinates.OrderBy(t => t.EuclideanDistance(pivotCoord)).ToList();
                    labels = mHGJumper.SampleSignificantEnrichmentVector(1e-3).ToList();
                    Console.WriteLine($"Instantiated sample with p={mHGJumper.minimumHypergeometric(labels.ToArray()).Item1:e} around pivot {pivotCoord.ToString()}");
                    mHGJumper.optHGT = 0.05;
                }
                instance_created = labels.Any();
            }
            if (save)
                Generics.SaveToCSV(coordinates.Zip(labels, (a, b) => a.ToString() + "," + Convert.ToDouble(b)),
                    $@"coords_{StaticConfigParams.filenamesuffix}.csv");
            return new Tuple<List<ICoordinate>, List<bool>>(coordinates, labels);
        }

    }
}
