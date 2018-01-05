using System.Linq;
using System;
using System.Net;
using System.Net.Http;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;
using SpatialEnrichmentWrapper;

namespace SpatialEnrichment
{
    public static class Function
    {
        [FunctionName("HttpTriggerCSharp")]
        public static async Task<HttpResponseMessage> Run([HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = null)]HttpRequestMessage req, TraceWriter log)
        {
            log.Info("C# HTTP trigger function processed a request.");

            // Get request body
            dynamic data = await req.Content.ReadAsAsync<object>();

            // Set name to query string or body data
            RequestClassSpots inputSpots = (RequestClassSpots)data?.spots.ToObject(typeof(RequestClassSpots));
            RequestClassParams inputParams = (RequestClassParams)data?.parameters.ToObject(typeof(RequestClassParams));

            List<Tuple<double, double, bool>> points = getPoints(inputSpots);
            Dictionary<string, string> parametersDictionary = convertParamsToDictionary(inputParams);

            //testing
           foreach(Tuple<double, double, bool> entry in points)
            {
                log.Info(entry.ToString());
            }

            //testing
            foreach(string str in parametersDictionary.Keys)
            {
                log.Info(str);
            }

            //testing
            foreach (string str in parametersDictionary.Values)
            {
                log.Info(str);
            }


            List<ISpatialmHGResult> spatialmHGResults = getSpatialmHGResults(points, parametersDictionary);
            log.Info("test2!!!!");
            ResponseClass output = new ResponseClass(convertResultsToResponse(spatialmHGResults));

            return inputSpots == null
                ? req.CreateResponse(HttpStatusCode.BadRequest, "Please pass a request body")
                : req.CreateResponse(HttpStatusCode.OK,  output.ToString());
        }

        private static Dictionary<string, string> convertParamsToDictionary(RequestClassParams inputParams)
        {
            Dictionary<string, string> parameters = new Dictionary<string, string>();
            parameters.Add("Action", inputParams.Actions);
            parameters.Add("SKIP_SLACK", inputParams.SkipSlack);
            parameters.Add("SIGNIFICANCE_THRESHOLD", inputParams.Threshold);
            parameters.Add("ExecutionTokenId", inputParams.ExecutionTokenId);

            return parameters;
        }

        private static SpatialmHGSpot[] convertResultsToResponse(List<ISpatialmHGResult> spatialmHGResultsList)
        {
            SpatialmHGSpot[] spatialmHGSpots = new SpatialmHGSpot[spatialmHGResultsList.Count];
            int i = 0;

            foreach (SpatialmHGResult result in spatialmHGResultsList)
            {
                spatialmHGSpots[i] = new SpatialmHGSpot(result.X, result.Y, result.mHGthreshold, result.pvalue);
                i++;
            }

            return spatialmHGSpots;
        }

        private static List<Tuple<double, double, bool>> getPoints(RequestClassSpots input)
        {
            List<Tuple<double, double, bool>> points = new List<Tuple<double, double, bool>>(input.Spots.Length);
            Spot[] spots = input.Spots;

            foreach (Spot spot in spots)
            {
                // notice 0 means true here!!!
                bool infoBool = spot.Info == 0;
                points.Add(Tuple.Create(spot.Lon, spot.Lat, infoBool));
            }

            return points;
        }

        private static List<ISpatialmHGResult> getSpatialmHGResults(List<Tuple<double, double, bool>> points, Dictionary<string, string> parameters)
        {
            EnrichmentWrapper wrapper = new EnrichmentWrapper(parameters);
            return wrapper.SpatialmHGWrapper(points);
        }
    }
}