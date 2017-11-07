using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SpatialEnrichmentWrapper;
using SpatialEnrichmentWrapper.Helpers;

namespace UnitTests
{
    [TestClass]
    public class AzureDB
    {
        [TestMethod]
        public void TestConnection()
        {
            var db = new DatabaseProgressQuery.DatabaseHandler();
            var q = new DatabaseProgressQuery.Query()
            {
                Id = "3",
                Message = "Initialized Log.",
                Value = 0
            };
            db.CreateQueryDocumentIfNotExistsAsync(q).Wait();
        }

        [TestMethod]
        public void TestFrontendCall()
        {
            var config = new ConfigParams(tokenId: "10");
            var er = new EnrichmentWrapper(config);
            var instance = RandomInstance.RandomizeCoordinatesAndSave(20, config, false);
            er.SpatialmHGWrapper(instance.Item1.Zip(instance.Item2,(a,b)=>new Tuple<double,double,bool>(a.GetDimension(0), a.GetDimension(1),b)).ToList());
        }
    }
}
