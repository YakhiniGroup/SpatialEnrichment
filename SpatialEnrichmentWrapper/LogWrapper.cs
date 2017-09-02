using System;
using System.Collections.Concurrent;
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
        private BlockingCollection<Tuple<string,int>> messageQueue;
        public LogWrapper(string token = "")
        {
            this.ExecutionTokenId = token;
            db = new DatabaseProgressQuery.DatabaseHandler();
            messageQueue = new BlockingCollection<Tuple<string, int>>();
            if (this.ExecutionTokenId == "") return;
            var q = new DatabaseProgressQuery.Query()
            {
                Id = ExecutionTokenId,
                Message = "Initialized Log.",
                Value = 0
            };
            var initTask = db.CreateQueryDocumentIfNotExistsAsync(q);
            UpdateDBTask(initTask);
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
            messageQueue.Add(new Tuple<string, int>(str, progress));
        }

        ~LogWrapper()
        {
            messageQueue.CompleteAdding();
        }

        private void UpdateDBTask(Task prevTask)
        {
            Task.Run(() =>
            {
                //var oldQ = db.SearchForQuery(ExecutionTokenId);
                foreach (var msg in messageQueue.GetConsumingEnumerable())
                {
                    prevTask.Wait();
                    var delTask = db.DeleteQueryDocumentAsync(ExecutionTokenId);
                    var q = new DatabaseProgressQuery.Query()
                    {
                        Id = ExecutionTokenId,
                        Message = msg.Item1,
                        Value = msg.Item2
                    };
                    delTask.Wait();
                    prevTask = db.CreateQueryDocumentIfNotExistsAsync(q);
                    //if (oldQ != null)
                    //    db.ReplaceQueryDocumentAsync(q, oldQ);
                }
            });

        }
    }
}
