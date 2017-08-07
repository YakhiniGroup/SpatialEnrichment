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
        public const string k_EndpointUrl = "<your endpoint URL>"; // Azure Portal -> Azure Cosmos DB account -> Keys
        public const string k_PrimaryKey = "<your primary key>"; // Azure Portal -> Azure Cosmos DB account -> Keys
        public const string k_DataBaseID = "<Sample DB ID>";
        public const string k_CollectionID = "<Sample Collection ID>";
        public DocumentClient client;

        public DatabaseHandler()
        {
            GetStarted();
        }

        private void GetStarted()
        {
            //connect to the Azure Cosmos DB account(REQUIRED!)
            this.client = new DocumentClient(new Uri(k_EndpointUrl), k_PrimaryKey);
        }


        public Query SearchForQuery(string id)
        {
            // Set some common query options - max return item is 1
            FeedOptions queryOptions = new FeedOptions { MaxItemCount = 1 };
            Query[] queryArry;

            //  find the query via its id
            IQueryable<Query> queryList = this.client.CreateDocumentQuery<Query>(
                    UriFactory.CreateDocumentCollectionUri(k_DataBaseID, k_CollectionID), queryOptions)
                    .Where(q => q.Id.Equals(id));
            queryArry = queryList.ToArray();

            return queryArry == null ? null : queryList.ToArray()[0];
        }

        public async Task CreateQueryDocumentIfNotExistsAsync(Query query)
        {
            try
            {
                await this.client.ReadDocumentAsync(UriFactory.CreateDocumentUri(k_DataBaseID, k_CollectionID, query.Id));
                // Trying to read - if successful document with the id already existed
            }
            catch (DocumentClientException de)
            {
                if (de.StatusCode == HttpStatusCode.NotFound)
                {
                    await this.client.CreateDocumentAsync(UriFactory.CreateDocumentCollectionUri(k_DataBaseID, k_CollectionID), query);
                    // if in this scope - document with the id does not exist, so it has been created
                }
                else
                {
                    throw;
                    // a faliure
                }
            }
        }

        public async Task ReplaceQueryDocumentAsync(Query newQuery, Query oldQuery)
        {
            await this.client.ReplaceDocumentAsync(UriFactory.CreateDocumentUri(k_DataBaseID, k_CollectionID, oldQuery.Id), newQuery);
        }


        public async Task DeleteQueryDocumentAsync(string id)
        {
            await this.client.DeleteDocumentAsync(UriFactory.CreateDocumentUri(k_DataBaseID, k_CollectionID, id));
        }
    }
}