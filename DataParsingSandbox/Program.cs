using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace DataParsingSandbox
{
    class Program
    {
        static void Main(string[] args)
        {
            ParseYeastRegulationDb();
        }

        public static void ParseYeastRegulationDb()
        {
            var chrNames = new Dictionary<string, int>() { { "chrI", 1 }, { "chrII", 2 }, { "chrIII", 3 }, { "chrIV", 4 }, { "chrV", 5 }, { "chrVI", 6 }, { "chrVII", 7 }, { "chrVIII", 8 }, { "chrIX", 9 }, { "chrX", 10 }, { "chrXI", 11 }, { "chrXII", 12 }, { "chrXIII", 13 }, { "chrXIV", 14 }, { "chrXV", 15 }, { "chrXVI", 16 } };
            string filename = @"C:\Users\shaybe\Dropbox\Thesis-PHd\SpatialEnrichment\Yeast\YeastGeneOntology.tsv";
            var TFGroups = File.ReadAllLines(filename).Select(l=>l.Split('\t')).GroupBy(l=>l[0]);
            foreach(var TFG in TFGroups.Where(g=>g.Count() > 5 && g.Count() < 1000))
            {
                File.WriteAllLines(TFG.Key.Replace(':','_') + ".csv", 
                    TFG.Where(r=>chrNames.ContainsKey(r[3])).Select(r => $"{chrNames[r[3]]},{r[2]}"));
            }
        }
    }
}
