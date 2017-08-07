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
                query = handler.SearchForQuery(id);
            }
            catch (DocumentClientException de)
            {
                Exception baseException = de.GetBaseException();
                log.Info(String.Format("{0} error occurred: {1}, Message: {2}", de.StatusCode, de.Message, baseException.Message));
            }
            catch (Exception e)
            {
                Exception baseException = e.GetBaseException();
                log.Info(String.Format("Error: {0}, Message: {1}", e.Message, baseException.Message));
            }
            finally
            {
                responseMesssage = query == null
                    ? req.CreateResponse(HttpStatusCode.BadRequest, "Error while querying data base")
                    : req.CreateResponse(HttpStatusCode.OK, query);
            }

            return responseMesssage;
        }
    }
}