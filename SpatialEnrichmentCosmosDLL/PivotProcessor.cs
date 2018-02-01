using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ScopeRuntime;
using SpatialEnrichment.Helpers;
using SpatialEnrichmentWrapper;

namespace SpatialEnrichmentCosmosDLL
{
    public class PivotProcessor : Processor
    {
        public static List<Coordinate3D> Coordinates = new List<Coordinate3D>();
        public static List<bool> Labels = new List<bool>();
        public PivotProcessor(string filename)
        {
            var coords = File.ReadAllLines(filename).Select(l => l.Split(',')).Select(sl =>
                new {Coord = new Coordinate3D(string.Join(",", sl.Take(3))), Label = sl[3] == "1"});
            foreach (var coord in coords)
            {
                Coordinates.Add(coord.Coord);
                Labels.Add(coord.Label);
            }
            mHGJumper.Initialize(Labels.Sum(v => v ? 1 : 0), Labels.Sum(v => v ? 0 : 1));
            mHGJumper.optHGT = 0.05;
        }

        public override Schema Produces(string[] requestedColumns, string[] args, Schema input)
        {
            return new Schema("Pivot:string,Pval:double,Thresh:int");
        }

        public override IEnumerable<Row> Process(RowSet input, Row outputRow, string[] args)
        {
            foreach (var row in input.Rows)
            {
                var pivot = (Coordinate3D) row[0].Value;
                var mHG = Gridding.EvaluatePivot(pivot, Coordinates, Labels);
                outputRow[0].Set(pivot.ToString());
                outputRow[1].Set(mHG.Item1);
                outputRow[2].Set(mHG.Item2);
                yield return outputRow;
            }
        }
    }

    public class PivotGenerator : Processor
    {
        public override Schema Produces(string[] requestedColumns, string[] args, Schema input)
        {
            return new Schema("Pivot:SpatialEnrichmentWrapper.Coordinate3D");
        }

        public override IEnumerable<Row> Process(RowSet input, Row outputRow, string[] args)
        {
            foreach (var row in input.Rows)
            {
                var inducers = (List<List<Coordinate3D>>)row[1].Value;
                var pivot = Gridding.GetPivotForCoordSet(inducers);
                outputRow[0].Set(pivot);
                yield return outputRow;
            }
        }
    }
}
