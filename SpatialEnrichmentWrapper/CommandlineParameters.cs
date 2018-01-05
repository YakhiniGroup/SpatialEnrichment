using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommandLine;

namespace SpatialEnrichmentWrapper
{
    public class CommandlineParameters
    {
        [Option('b',"BatchMode", DefaultValue = false, HelpText = @"Running on Azure batch", Required = false)]
        public bool BatchMode { get; set; }

        [Option('u', "SaasUrl", DefaultValue = "", HelpText = @"Azure batch storage Saas url", Required = false)]
        public string SaasUrl { get; set; }
    }
}
