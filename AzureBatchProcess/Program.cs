using Newtonsoft.Json;
using SpatialEnrichmentWrapper;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AzureBatchProcess
{
    class Program
    {
        static void Main(string[] args)
        {
            List<string> filelist;
            string targetdir = args[0];
            if ((File.GetAttributes(args[0]) & FileAttributes.Directory) == FileAttributes.Directory)
            {
                filelist = Directory.EnumerateFiles(args[0], "*.csv")
                    .Where(f => !File.Exists(Path.ChangeExtension(f, ".res"))).ToList();
            }
            else
            {
                if (args.Last() == "--shuffle")
                {
                    filelist = new List<string>();
                    var rnd = new Random();
                    var data = File.ReadAllLines(args[0]).Select(l => l.Split(',')).ToList();
                    var labels = data.Select(l => l.Last()).ToList();
                    var coordinates = data.Select(l => string.Join(",", l.Reverse().Skip(1).Reverse())).ToList();
                    var filepath = Path.GetDirectoryName(args[0]);
                    targetdir = Path.Combine(filepath, "perms");
                    if (!Directory.Exists(targetdir))
                        Directory.CreateDirectory(targetdir);
                    var filename = Path.GetFileNameWithoutExtension(args[0]);
                    for (var i = 0; i < 100; i++)
                    {
                        var currlabels=labels.OrderBy(v => rnd.NextDouble()).ToList();
                        var tgtfilename = $"{Path.Combine(targetdir, filename)}_prm_{i}.csv";
                        File.WriteAllLines(tgtfilename, coordinates.Zip(currlabels,(a,b)=>a+","+b));
                        filelist.Add(tgtfilename);
                    }
                }
                else if (args.Last() == "--exhaust")
                {
                    AzureBatchExecution.SubtaskDuration = 120.0;
                    targetdir= Path.GetDirectoryName(args[0]);
                    filelist = new List<string> { args[0] }; 
                }
                else
                    filelist = new List<string> {args[0]};
            }

            Dictionary<string, string> AzureConfigSource = null;
            if (args.Length > 1 && File.Exists(args[1]) && Path.GetExtension(args[1]) == ".config")
            {
                AzureConfigSource = JsonConvert.DeserializeObject<Dictionary<string, string>>(File.ReadAllText(args[1]));
                Console.WriteLine($"Using azure configuration file {args[1]}");
            }
            else
            {
                AzureConfigSource = JsonConvert.DeserializeObject<Dictionary<string, string>>(File.ReadAllText("account1.config"));
                Console.WriteLine($"Using azure configuration file account1.config");
            }
            AzureBatchExecution.LoadAzureConfig(AzureConfigSource);

            var job = new AzureBatchExecution(targetdir);
            Console.WriteLine($"Using Batch Account: {AzureBatchExecution.BatchAccountUrl}");
            Console.WriteLine($"Using Storage Account: {AzureBatchExecution.StorageAccountName}");
            var task = job.MainAsync(filelist);
            task.Wait();
            Console.WriteLine("Done.");
            Console.ReadKey();

        }
    }
}
