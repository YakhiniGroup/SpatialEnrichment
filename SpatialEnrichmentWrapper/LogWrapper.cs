using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DatabaseProgressQuery;

namespace SpatialEnrichmentWrapper
{
    public class LogWrapper
    {
        private string ExecutionTokenId = "";
        private DatabaseHandler db;
        private BlockingCollection<Tuple<string,int>> messageQueue;
        public Task updater;
        private Query prevQuery;
        public LogWrapper(string token = "")
        {
            this.ExecutionTokenId = token;
            if (this.ExecutionTokenId == "") return;
            db = new DatabaseHandler();
            messageQueue = new BlockingCollection<Tuple<string, int>>();
            prevQuery = new Query()
            {
                Id = ExecutionTokenId,
                Message = "Initialized Log.",
                Value = 0
            };
            var initTask = db.CreateQueryDocumentIfNotExistsAsync(prevQuery);
            UpdateDBTask(initTask);
        }

        public void Seal()
        {
            messageQueue?.CompleteAdding();
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
            messageQueue?.CompleteAdding();
        }

        private void UpdateDBTask(Task prevTask)
        {
            updater = Task.Run(() =>
            {
                //var oldQ = db.SearchForQuery(ExecutionTokenId);
                Tuple<string, int> lastmsg;
                foreach (var msg in messageQueue.GetConsumingEnumerable())
                {
                    prevTask.Wait();
                    lastmsg = msg;
                    int skipcounter = 0;
                    while (messageQueue.TryTake(out var tmpmsg, TimeSpan.FromSeconds(1)) && skipcounter < 100)
                    {
                        skipcounter++;
                        lastmsg = tmpmsg;
                    }
                    //var delTask = db.DeleteQueryDocumentAsync(ExecutionTokenId);
                    var q = new DatabaseProgressQuery.Query()
                    {
                        Id = ExecutionTokenId,
                        Message = lastmsg.Item1,
                        Value = lastmsg.Item2
                    };
                    //delTask.Wait();
                    //prevTask = db.CreateQueryDocumentIfNotExistsAsync(q);
                    if (prevQuery != null)
                        prevTask = db.ReplaceQueryDocumentAsync(q, prevQuery);
                    prevQuery = q;
                }
            });

        }
    }
}
