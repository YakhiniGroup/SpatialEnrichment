using System;
using System.Linq;
using System.Threading.Tasks;

using System.Net;
using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;
using Newtonsoft.Json;

public class Program
{
    private const string EndpointUrl = "<your endpoint URL>"; // Azure Portal -> Azure Cosmos DB account -> Keys
    private const string PrimaryKey = "<your primary key>"; // Azure Portal -> Azure Cosmos DB account -> Keys
    private const string SampleDBID = "Sample_DB_ID"; // based on the manualy created data base
    private const string SampleCollectionID = "Sample_Collection_ID"; // based on the manualy created collection
    private DocumentClient client;

    // THE QUERY
    public class Query
    {
        [JsonProperty(PropertyName = "id")]
        public string Id { get; set; }
        public int Value { get; set; }
        public string Message{ get; set; }
        public override string ToString()
        {
            return JsonConvert.SerializeObject(this);
        }
    }

    static void Main(string[] args)
    {
        try
        {
                Program p = new Program();
                p.GetStartedDemo().Wait();
        }
        catch (DocumentClientException de)
        {
                Exception baseException = de.GetBaseException();
        }
        catch (Exception e)
        {
                Exception baseException = e.GetBaseException();
        }
        finally
        {
                
        }
    }

    private async Task GetStartedDemo()
    {
        //connect to the Azure Cosmos DB account(REQUIRED!)
        this.client = new DocumentClient(new Uri(EndpointUrl), PrimaryKey);

        // sample query objects - need to create these from the algorithm
        Query sampleQuery = new Query
        {
            Id = "testId",
            Value = 20,
            Message = "Processed data"
        };

        Query updatedSampleQuery = new Query
        {
            //SAME ID
            Id = "testId",
            Value = 40,
            Message = "finished stage 2"
        };

        //creating the JSON document in the data base (REQUIRED!)
        await this.CreateQueryDocumentIfNotExists(SampleDBID, SampleCollectionID, sampleQuery);

        //example of replacing a JSON document in the data base 
        await this.client.ReplaceDocumentAsync(UriFactory.CreateDocumentUri(sampleDBID, SampleCollectionID, sampleQuery.Id), updatedSampleQuery);
    }

    private async Task CreateQueryDocumentIfNotExists(string databaseName, string collectionName, Query query)
    {
        try
        {
            await this.client.ReadDocumentAsync(UriFactory.CreateDocumentUri(databaseName, collectionName, query.Id));
            // Trying to read - if successful document with the id already existed
        }
        catch (DocumentClientException de)
        {
            if (de.StatusCode == HttpStatusCode.NotFound)
            {
                await this.client.CreateDocumentAsync(UriFactory.CreateDocumentCollectionUri(databaseName, collectionName), query);
                // if in this scope - document with the id does not exist, so it has been created
            }
            else
            {
                throw;
                // a faliure
            }
        }
    }
}