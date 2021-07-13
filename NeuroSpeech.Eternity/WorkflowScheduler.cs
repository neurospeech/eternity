using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace NeuroSpeech.Eternity
{
    public class WorkflowScheduler<T>
    {
        class JobItem
        {
            public readonly string Key;
            public readonly Func<Task> Task;
            public JobItem(string key, Func<Task> task)
            {
                this.Key = key;
                this.Task = task;
            }
        }

        private ConcurrentDictionary<string, ConcurrentQueue<JobItem>> jobs 
            = new ConcurrentDictionary<string, ConcurrentQueue<JobItem>>();

        private BlockingCollection<ConcurrentQueue<JobItem>>[] Pool;

        public WorkflowScheduler(int threads = 10)
        {
            Pool = new BlockingCollection<ConcurrentQueue<JobItem>>[threads];
            for (int i = 0; i < threads; i++)
            {
                var tasks = new BlockingCollection<ConcurrentQueue<JobItem>>();
                Pool[i] = tasks;
                Task.Run(() => RunAsync(tasks));
            }
        }

        private static async Task RunAsync(BlockingCollection<ConcurrentQueue<JobItem>> tasks)
        {
            foreach(var item in tasks.GetConsumingEnumerable())
            {
                while(item.TryDequeue(out var qi))
                {
                    await qi.Task();
                }
            }
        }

        public void Queue(T item, Func<T,string> keyFunc) {
            var key = keyFunc(item);

        }

    }
}
