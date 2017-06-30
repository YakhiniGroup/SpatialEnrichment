using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SpatialEnrichment;
using SpatialEnrichmentWrapper;

namespace UsageExample
{
    class Program
    {
        static void Main(string[] args)
        {
            var input = new List<Tuple<double, double, bool>>();
            for (var i = 0; i < 20; i++)
            {
                var coord = Coordinate.MakeRandom();
                var label = StaticConfigParams.rnd.NextDouble() > StaticConfigParams.CONST_NEGATIVELABELRATE;
                input.Add(new Tuple<double, double, bool>(coord.GetDimension(0), coord.GetDimension(1), label));
            }
            var ew = new EnrichmentWrapper(new ConfigParams());
            var res = ew.SpatialmHGWrapper(input);

            Console.WriteLine("Printing results:");
            foreach (var spatialmHgResult in res)
            {
                Console.WriteLine("");
            }

        }
    }
}
