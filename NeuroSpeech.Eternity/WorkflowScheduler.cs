using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NeuroSpeech.Eternity
{
    public class WorkflowScheduler<T> : IDisposable
    {
        private readonly CancellationToken cancellationToken;
        private ConcurrentDictionary<string, Task> pendingTasks
            = new ConcurrentDictionary<string, Task>();

        public WorkflowScheduler(int n = 0, CancellationToken cancellationToken = default)
        {
            this.cancellationToken = cancellationToken;
        }



        public void Dispose()
        {
            
        }

        internal Task Queue(string id, 
            T item, 
            Func<T, CancellationToken, Task> runWorkflowAsync)
        {
            return pendingTasks.AddOrUpdate(id, 
                k => RunTask(k, item, runWorkflowAsync, null), 
                (k, old) => RunTask(k, item, runWorkflowAsync, old));

        }

        private async Task RunTask(string id, T item, Func<T, CancellationToken, Task> runWorkflowAsync, Task? previous)
        {
            if(previous != null)
            {
                await previous;
            }
            await runWorkflowAsync(item, cancellationToken);
            pendingTasks.TryRemove(id, out var none);
        }
    }


    //public class WorkflowScheduler<T>: IDisposable
    //{
    //    class JobItem
    //    {
    //        public readonly string Key;
    //        public readonly T Arg;
    //        public readonly Func<T, CancellationToken, Task> Task;
    //        private readonly TaskCompletionSource<int> CompletionSource;
    //        public readonly Task Completion;

    //        public JobItem(string key, T arg, Func<T, CancellationToken, Task> task)
    //        {
    //            this.Key = key;
    //            this.Arg = arg;
    //            this.Task = task;
    //            this.CompletionSource = new TaskCompletionSource<int>();
    //            this.Completion = this.CompletionSource.Task;
    //        }

    //        public void Finish(Exception? ex = null)
    //        {
    //            if (ex == null)
    //            {
    //                CompletionSource.TrySetResult(0);
    //            } else
    //            {
    //                CompletionSource.TrySetException(ex);
    //            }
    //        }
    //    }

    //    private static List<SingleThreadTaskScheduler<T>> schedulers = new List<SingleThreadTaskScheduler<T>>();
    //    private readonly int maxThreads;
    //    private readonly CancellationToken cancellationToken;
    //    private ConcurrentDictionary<string, SingleThreadTaskScheduler<T>> jobs 
    //        = new ConcurrentDictionary<string, SingleThreadTaskScheduler<T>>();

    //    public WorkflowScheduler(int maxThreads, CancellationToken cancellationToken)
    //    {
    //        cancellationToken.Register(() => {
    //            this.Dispose();
    //        });
    //        this.maxThreads = maxThreads;
    //        this.cancellationToken = cancellationToken;
    //    }

    //    public Task Queue(string key, T arg, Func<T, CancellationToken, Task> item) {
    //        var q = jobs.GetOrAdd(key, (k1) =>
    //        {
    //            lock (jobs)
    //            {
    //                return jobs.GetOrAdd(k1, k2 =>
    //                {
    //                    lock (schedulers)
    //                    {
    //                        return TaskScheduler();
    //                    }
    //                });
    //            }
    //        });

    //        q.Factory.StartNew()

    //        var ji = new JobItem(key, arg, item, q);
    //        q.Enqueue(ji);

    //        var first = Pool.FirstOrDefault(x => x.Count == 0);
    //        if(first == null)
    //        {
    //            if (Pool.Count < maxThreads)
    //            {
    //                var tasks = new BlockingCollection<ConcurrentQueue<JobItem>>();
    //                Pool.Add(tasks);
    //                Task.Factory.StartNew(() => RunAsync(tasks), TaskCreationOptions.LongRunning);
    //                first = tasks;
    //            }
    //            else
    //            {
    //                first = Pool.OrderBy(x => x.Count).First();
    //            }
    //        }
    //        first.Add(q);
    //        return ji.Completion;
    //    }

    //    private static SingleThreadTaskScheduler TaskScheduler()
    //    {
    //        lock (schedulers)
    //        {
    //            var first = schedulers.OrderBy(x => x.Count).FirstOrDefault();
    //            if(first == null || (first.Count > 0 && schedulers.Count < 10))
    //            {
    //                first = new SingleThreadTaskScheduler();
    //                schedulers.Add(first);
    //            }
    //            return first;
    //        }
    //    }

    //    public void Dispose()
    //    {
    //    }
    //}
}
