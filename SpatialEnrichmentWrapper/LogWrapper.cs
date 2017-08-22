using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SpatialEnrichmentWrapper
{
    public class LogWrapper
    {
        private string ExecutionTokenId = "";
        private DatabaseProgressQuery.DatabaseHandler db;
        public LogWrapper(string token = "")
        {
            this.ExecutionTokenId = token;
            db = new DatabaseProgressQuery.DatabaseHandler();
        }

        public void WriteLine(string format, params object[] values)
        {
            WriteLine(-1, format,  values);
        }

        public void WriteLine(int progress = 0, string format="",  params object[] values)
        {
            var str = string.Format(format, values);
            Console.WriteLine(str);
            if (this.ExecutionTokenId == "") return;
            Task.Run(() =>
            {
                var oldQ = db.SearchForQuery(ExecutionTokenId);
                var q = new DatabaseProgressQuery.Query()
                {
                    Id = ExecutionTokenId,
                    Message = str,
                    Value = progress >= 0 ? progress : oldQ.Value
                };
                if (oldQ != null)
                    db.ReplaceQueryDocumentAsync(q, oldQ);
            });
        }
    }
}
