using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommandLine;

namespace SpatialEnrichmentWrapper
{
    public class CommandlineParameters
    {
        [Option('d', "DurationMinutes", DefaultValue = 5.0, HelpText = @"Running time cap", Required = false)]
        public double Duration { get; set; }

        [Option('i', "InputFile", HelpText = @"Input file to analyze", Required = true)]
        public string InputFile { get; set; }

        [Option('b',"BatchMode", DefaultValue = false, HelpText = @"Running on Azure batch", Required = false)]
        public bool BatchMode { get; set; }

        [Option('u', "SaasUrl", DefaultValue = "", HelpText = @"Azure batch storage Saas url", Required = false)]
        public string SaasUrl { get; set; }

        [Option('s', "GenomeStructure", DefaultValue = "", HelpText = @"semicolon (;) delimited per chromsome bin count", Required = false)]
        public string GenomeStructure { get; set; }
    }
}
