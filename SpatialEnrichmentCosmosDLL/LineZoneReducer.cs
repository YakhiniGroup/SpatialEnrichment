using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ScopeRuntime;
using SpatialEnrichment.Helpers;
using SpatialEnrichmentWrapper;
using SpatialEnrichmentWrapper.Helpers;
using Accord.Math;

namespace SpatialEnrichmentCosmosDLL
{
    public class LineZoneReducer : Processor
    {
        public static List<List<List<Coordinate3D>>> InducerPairs;
        public LineZoneReducer(string filename)
        {
            var coords = File.ReadAllLines(filename).Select(l => l.Split(',')).Select(sl =>
                    new {Coord = new Coordinate3D(string.Join(",", sl.Take(3))), Label = sl[3] == "1"})
                .GroupBy(v => v.Label).ToDictionary(v => v.Key, v => v.Select(x=>x.Coord).ToList());
            var inducerPairs = new List<List<Coordinate3D>>();
            foreach (var pos in coords[true])
            foreach (var neg in coords[false])
                inducerPairs.Add(new List<Coordinate3D>() {pos, neg});
            InducerPairs = inducerPairs.DifferentCombinations(2).Select(pair => pair.ToList()).ToList();
        }

        public override Schema Produces(string[] requestedColumns, string[] args, Schema input)
        {
            return new Schema("Pivot:Coordinate3D");
        }

        public override IEnumerable<Row> Process(RowSet input, Row outputRow, string[] args)
        {
            var r = new Accord.Math.Rational(1.0);
            foreach (var row in input.Rows)
            {
                var inputLine = (List<Coordinate3D>) row[0].Value;
                foreach(var inducers in InducerPairs.Where(inducers => TupleDistinct(inputLine, inducers[0], inducers[1])))
                {
                    var pivot = Gridding.GetPivotForCoordSet(new List<List<Coordinate3D>> {inputLine, inducers[0], inducers[1]});
                    outputRow[0].Set((Coordinate3D)pivot);
                    yield return outputRow;
                }

            }
        }

        public static bool TupleDistinct(List<Coordinate3D> A, List<Coordinate3D> B, List<Coordinate3D> C)
        {
            return
                (!A[0].Equals(B[0]) || !A[1].Equals(B[1])) &&
                (!A[0].Equals(C[0]) || !A[1].Equals(C[1])) &&
                (!B[0].Equals(C[0]) || !B[1].Equals(C[1]));
        }
    }
}
