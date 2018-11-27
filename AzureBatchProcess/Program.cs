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
            AzureBatchExecution.BatchAccountName = ConfigurationManager.AppSettings["BatchAccountName"];
            AzureBatchExecution.BatchAccountKey = ConfigurationManager.AppSettings["BatchAccountKey"];
            AzureBatchExecution.BatchAccountUrl = ConfigurationManager.AppSettings["BatchAccountUrl"];
            Console.WriteLine($"Using Batch Account: {AzureBatchExecution.BatchAccountUrl}");
            var job = new AzureBatchExecution(args[0]);
            var task = job.MainAsync(filelist);
            task.Wait();
            Console.WriteLine("Done.");
            Console.ReadKey();

        }
    }
}
