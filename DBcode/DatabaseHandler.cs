using System;
using System.Linq;
using System.Threading.Tasks;

using System.Net;
using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;
using Newtonsoft.Json;

namespace DatabaseProgressQuery
{
    public class DatabaseHandler
    {
        public const string EndpointUrl = "<your endpoint URL>"; // Azure Portal -> Azure Cosmos DB account -> Keys
        public const string PrimaryKey = "<your primary key>"; // Azure Portal -> Azure Cosmos DB account -> Keys
        public const string DataBaseID = "<Sample DB ID>";
        public const string CollectionID = "<Sample Collection ID>";
        public DocumentClient client;

        public DatabaseHandler()
        {
            GetStarted().Wait();
        }

        private async Task GetStarted()
        {
            //connect to the Azure Cosmos DB account(REQUIRED!)
            this.client = new DocumentClient(new Uri(EndpointUrl), PrimaryKey);
        }


        public Query ExecuteSimpleQuery(string databaseName, string collectionName, string id)
        {
            // Set some common query options
            FeedOptions queryOptions = new FeedOptions { MaxItemCount = 1 };

            //  find the query via its id
            IQueryable<Query> queryList = this.client.CreateDocumentQuery<Query>(
                    UriFactory.CreateDocumentCollectionUri(databaseName, collectionName), queryOptions)
                    .Where(q => q.Id.Equals(id));
            return queryList == null || queryList.Count == 0 ? null : queryList[0];
        }
    }
}