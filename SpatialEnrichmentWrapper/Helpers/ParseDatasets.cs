using System.IO;
using System.Linq;

namespace SpatialEnrichment.Helpers
{
    public static class ParseDatasets
    {
        public static void ParseUSCapitals()
        {
            int count = 0;
            string name="", capname="", x="", y="";
            using (var fout = new StreamWriter(@"c:\Users\shaybe\Dropbox\Thesis-PHd\SpatialEnrichment\Datasets\usStates_parsed.csv"))
                foreach (var line in
                        File.ReadLines(@"c:\Users\shaybe\Dropbox\Thesis-PHd\SpatialEnrichment\Datasets\usStates.csv")
                            .Where(l => !l.StartsWith("#")))
                {
                    var sl = line.Split(':');
                    switch (count)
                    {
                        case 0:
                            name = sl[1];
                            break;
                        case 1:
                            capname = sl[1];
                            break;
                        case 2:
                            x = sl[1];
                            break;
                        case 3:
                            y = sl[1];
                            fout.WriteLine(@"{0} - {1},{2},{3}",capname,name,x,y);
                            break;
                    }
                    count = (count + 1) % 4;
                }
        }
    }
}
