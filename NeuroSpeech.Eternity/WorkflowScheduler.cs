using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NeuroSpeech.Eternity
{
    public class WorkflowScheduler<T>
    {
        class JobItem
        {
            public readonly string Key;
            public readonly T Arg;
            public readonly Func<T, CancellationToken, Task> Task;
            public readonly ConcurrentQueue<JobItem> Queue;
            private readonly TaskCompletionSource<int> CompletionSource;
            public readonly Task Completion;

            public JobItem(string key, T arg, Func<T, CancellationToken, Task> task, ConcurrentQueue<JobItem> queue)
            {
                this.Key = key;
                this.Arg = arg;
                this.Task = task;
                this.Queue = queue;
                this.CompletionSource = new TaskCompletionSource<int>();
                this.Completion = this.CompletionSource.Task;
            }

            public void Finish(Exception? ex = null)
            {
                if (ex == null)
                {
                    CompletionSource.TrySetResult(0);
                } else
                {
                    CompletionSource.TrySetException(ex);
                }
            }
        }

        private ConcurrentDictionary<string, ConcurrentQueue<JobItem>> jobs 
            = new ConcurrentDictionary<string, ConcurrentQueue<JobItem>>();

        private List<BlockingCollection<ConcurrentQueue<JobItem>>> Pool = new List<BlockingCollection<ConcurrentQueue<JobItem>>>();
        private readonly int maxThreads;
        private readonly CancellationToken cancellationToken;

        public WorkflowScheduler(int maxThreads, CancellationToken cancellationToken)
        {
            cancellationToken.Register(() => {
                foreach (var item in Pool)
                {
                    item.CompleteAdding();
                }
            });
            this.maxThreads = maxThreads;
            this.cancellationToken = cancellationToken;
        }

        private async Task RunAsync(BlockingCollection<ConcurrentQueue<JobItem>> tasks)
        {
            try
            {
                foreach (var item in tasks.GetConsumingEnumerable(cancellationToken))
                {
                    if (item.TryDequeue(out var qi))
                    {
                        try
                        {
                            await qi.Task(qi.Arg, cancellationToken);
                            qi.Finish();
                        }
                        catch (Exception ex)
                        {
                            qi.Finish(ex);
                        }
                        if (item.IsEmpty)
                        {
                            jobs.TryRemove(qi.Key, out var _);
                        }
                    }
                }
            }catch (TaskCanceledException) { }
        }

        public Task Queue(string key, T arg, Func<T, CancellationToken, Task> item) {
            var q = jobs.GetOrAdd(key, (k1) =>
            {
                lock (jobs)
                {
                    return jobs.GetOrAdd(k1, k2 =>
                    {
                        return new ConcurrentQueue<JobItem>();
                    });
                }
            });
            var ji = new JobItem(key, arg, item, q);
            q.Enqueue(ji);

            var first = Pool.FirstOrDefault(x => x.Count == 0);
            if(first == null)
            {
                if (Pool.Count < maxThreads)
                {
                    var tasks = new BlockingCollection<ConcurrentQueue<JobItem>>();
                    Pool.Add(tasks);
                    Task.Factory.StartNew(() => RunAsync(tasks), TaskCreationOptions.LongRunning);
                    first = tasks;
                }
                else
                {
                    first = Pool.OrderBy(x => x.Count).First();
                }
            }
            first.Add(q);
            return ji.Completion;
        }

    }
}
