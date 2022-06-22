using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NeuroSpeech.Eternity
{

    public class TaskDispatcher: IDisposable
    {
        public void Add(Func<Task> task)
        {
            tasks.Enqueue(task);
            waiting?.Cancel();
        }

        public int Count => tasks.Count;

        public TaskDispatcher()
        {
            Task.Run(RunSync);
        }

        private ConcurrentQueue<Func<Task>> tasks = new ConcurrentQueue<Func<Task>>();
        private CancellationTokenSource disposed = new CancellationTokenSource();
        private CancellationTokenSource? waiting = null;

        private async Task RunSync()
        {
            while(!disposed.IsCancellationRequested)
            {
                while(tasks.TryDequeue(out var task))
                {
                    await task();
                }
                var c = new CancellationTokenSource();
                waiting = c;
                try
                {
                    await Task.Delay(5000, c.Token);
                } catch (TaskCanceledException)
                {

                }
            }
        }

        public void Dispose()
        {
            disposed.Cancel();
        }
    }

    public class WorkflowQueue : IDisposable
    {
        private readonly CancellationTokenSource cancellationTokenSource;
        private readonly CancellationTokenRegistration registration;
        private ConcurrentDictionary<string, Task> pendingTasks
            = new ConcurrentDictionary<string, Task>();

        public WorkflowQueue(CancellationToken cancellationToken = default)
        {
            this.cancellationTokenSource = new CancellationTokenSource();
            this.registration = cancellationToken.Register(Dispose);
        }



        public void Dispose()
        {
            if (!cancellationTokenSource.IsCancellationRequested)
                cancellationTokenSource.Cancel();
            cancellationTokenSource.Dispose();
            registration.Dispose();
        }

        public void ClearCompleted()
        {
            foreach(var item in pendingTasks.ToList())
            {
                if(item.Value.IsCompleted || item.Value.IsFaulted || item.Value.IsCanceled)
                {
                    pendingTasks.TryRemove(item.Key, out var _);
                }
            }
        }

        public async Task WaitForAll()
        {
            await Task.WhenAll(pendingTasks.Values);
            pendingTasks.Clear();
        }

        ///// <summary>
        ///// This will queue all tasks and will wait till 50% of them are done.
        ///// </summary>
        ///// <param name="items"></param>
        ///// <param name="runWorkflowAsync"></param>
        ///// <returns></returns>
        //internal Task<int> QueueAny(WorkflowQueueItem[] items, Func<WorkflowQueueItem, CancellationToken, Task> runWorkflowAsync)
        //{
        //    var waiter = new TaskCompletionSource<int>();
        //    int done = 0;
        //    int max = items.Length / 2;
        //    // var tasks = new Task[items.Length];
        //    for (int i = 0; i < items.Length; i++)
        //    {
        //        var item = items[i];

        //        pendingTasks.AddOrUpdate(item.ID, 
        //            (x) => Task.Run(async () =>
        //            {
        //                try
        //                {
        //                    await runWorkflowAsync(item, cancellationTokenSource.Token);
        //                } finally
        //                {
        //                    Interlocked.Increment(ref done);
        //                    if(done >= max)
        //                    {
        //                        waiter.TrySetResult(pendingTasks.Count);
        //                    }
        //                }
        //            }),
        //            (x, existing) => Task.Run(async () => {
        //                try
        //                {
        //                    await existing;
        //                    await runWorkflowAsync(item, cancellationTokenSource.Token);
        //                } finally
        //                {
        //                    Interlocked.Increment(ref done);
        //                    if (done >= max)
        //                    {
        //                        waiter.TrySetResult(pendingTasks.Count);
        //                    }
        //                }
        //            }));
                
        //    }
        //    return waiter.Task;
        //}
    }

    public class WorkflowScheduler<T> : IDisposable
    {
        private readonly CancellationTokenSource cancellationTokenSource;
        private readonly CancellationTokenRegistration registration;
        private Dictionary<string, Task> pendingTasks
            = new Dictionary<string, Task>();

        public WorkflowScheduler(CancellationToken cancellationToken = default)
        {
            this.cancellationTokenSource = new CancellationTokenSource();
            this.registration = cancellationToken.Register(Dispose);
        }



        public void Dispose()
        {
            if (!cancellationTokenSource.IsCancellationRequested)
                cancellationTokenSource.Cancel();
            cancellationTokenSource.Dispose();
            registration.Dispose();
        }

        internal Task Queue(string id, 
            T item, 
            Func<T, CancellationToken, Task> runWorkflowAsync)
        {
            // return Task.Run(() => runWorkflowAsync(item, this.cancellationTokenSource.Token));
            lock (pendingTasks)
            {
                pendingTasks.TryGetValue(id, out var currentTask);
                var ct = currentTask;
                currentTask = Task.Run(async () =>
                {
                    if (ct != null)
                    {
                        await ct;
                    }
                    await runWorkflowAsync(item, cancellationTokenSource.Token);
                });
                pendingTasks[id] = currentTask;
                return currentTask;
            }
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
