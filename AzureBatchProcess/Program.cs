using SpatialEnrichmentWrapper;
using System;
using System.Collections.Generic;
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
            AzureBatchExecution.MainAsync(filelist).Wait();
            Console.WriteLine("Done.");
            Console.ReadKey();

        }
    }
}
