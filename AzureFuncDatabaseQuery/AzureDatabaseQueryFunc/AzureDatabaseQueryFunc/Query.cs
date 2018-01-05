using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;


namespace DatabaseProgressQuery
{
    public class Query
    {
        [JsonProperty(PropertyName = "id")]
        public string Id { get; set; }
        public int Value { get; set; } // 0 - 100
        public string Message{ get; set; }
        public override string ToString()
        {
            return JsonConvert.SerializeObject(this);
        }
    }
}
