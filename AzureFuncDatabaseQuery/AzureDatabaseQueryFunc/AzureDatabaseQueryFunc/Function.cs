using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System;

using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;

using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;
using Newtonsoft.Json;

namespace DatabaseProgressQuery
{
    public static class Function
    {
        [FunctionName("HttpTriggerCSharp")]
        public static async Task<HttpResponseMessage> Run([HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = null)]HttpRequestMessage req, TraceWriter log)
        {
            log.Info("C# HTTP trigger function processed a request.");

            dynamic data = await req.Content.ReadAsAsync<object>();
            HttpResponseMessage responseMesssage = null;
            string id = data?.id;
            Query query = null;

            try
            {
                DatabaseHandler handler = new DatabaseHandler();
                //Query testQuery1 = new Query { Id = "1", Value = 0, Message = "Proccess Started" };
                //Query testQuery2 = new Query { Id = "2", Value = 50, Message = "Proccessed data" };
                //Query testQuery3 = new Query { Id = "3", Value = 100, Message = "Proccess Complete" };
                //await handler.CreateQueryDocumentIfNotExistsAsync(testQuery1);
                //await handler.CreateQueryDocumentIfNotExistsAsync(testQuery2);
                //await handler.CreateQueryDocumentIfNotExistsAsync(testQuery3);
                query = handler.SearchForQuery(id);
                //await handler.DeleteQueryDocumentAsync("1");
                //await handler.DeleteQueryDocumentAsync("2");
                //await handler.DeleteQueryDocumentAsync("3");
            }
            catch (DocumentClientException de)
            {
                Exception baseException = de.GetBaseException();
                log.Info("---ERROR DocumentClientException---");
                log.Info(String.Format("{0} error occurred: {1}, Message: {2}", de.StatusCode, de.Message, baseException.Message));
            }
            catch (Exception e)
            {
                log.Info("---ERROR Exception---");
                Exception baseException = e.GetBaseException();
                log.Info(String.Format("Error: {0}, Message: {1}", e.Message, baseException.Message));
            }
            finally
            {
                Query notFoundQuery = new Query { Id = "-1", Value = 100, Message = "" };
                responseMesssage = query == null
                    ? req.CreateResponse(HttpStatusCode.OK, notFoundQuery)
                    : req.CreateResponse(HttpStatusCode.OK, query);
            }

            return responseMesssage;
        }
    }
}