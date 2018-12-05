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
            var filelist = Directory.EnumerateFiles(args[0], "*.csv").Where(f=>!File.Exists(Path.ChangeExtension(f,".res"))).ToList();
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

            var job = new AzureBatchExecution(args[0]);
            Console.WriteLine($"Using Batch Account: {AzureBatchExecution.BatchAccountUrl}");
            Console.WriteLine($"Using Storage Account: {AzureBatchExecution.StorageAccountName}");
            var task = job.MainAsync(filelist);
            task.Wait();
            Console.WriteLine("Done.");
            Console.ReadKey();

        }
    }
}
