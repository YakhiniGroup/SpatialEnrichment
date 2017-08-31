using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

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
    }
}
