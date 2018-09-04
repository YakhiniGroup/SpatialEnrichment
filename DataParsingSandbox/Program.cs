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
            //ParseYeastRegulationDb();
            ParseResFileTrio(@"d:\Dropbox\Thesis-PHd\SpatialEnrichment\Yeast\Evaluate\");
        }

        public static void ParseResFileTrio(string rootpath)
        {
            var gridfiles = Directory.EnumerateFiles(rootpath, "*.Grid.res");
            var sampfiles = Directory.EnumerateFiles(rootpath, "*.Sampling.res");
            var pivtfiles = Directory.EnumerateFiles(rootpath, "*.Pivot.res");
            var allfilegroups = gridfiles.Concat(sampfiles).Concat(pivtfiles).GroupBy(fn => fn.Split('.').First());
            var dict = new Dictionary<string, Tuple<double, double, double>>();
            foreach (var fg in allfilegroups)
            {
                var grid_res = 1.0;
                var pivt_res = 1.0;
                var samp_res = 1.0;

                foreach (var f in fg)
                {
                    var tres = double.Parse(File.ReadLines(f).First().Trim(new[] { '(', ')' }).Split(',')[0]);
                    if (f.Contains(".Grid."))
                        grid_res = tres;
                    else if (f.Contains(".Sampling."))
                        samp_res = tres;
                    else if (f.Contains(".Pivot."))
                        pivt_res = tres;
                }
                var res = new Tuple<double, double, double>(grid_res,pivt_res,samp_res);
                dict.Add(fg.Key, res);
            }
            File.WriteAllLines(rootpath + "aggregated.csv", dict.Select(kvp => kvp.Key + "," + kvp.Value.Item1 + "," + kvp.Value.Item2 + "," + kvp.Value.Item3));

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
