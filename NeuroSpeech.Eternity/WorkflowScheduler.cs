using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NeuroSpeech.Eternity
{

    public class TaskDispatcher
    {
        public void Add(Func<Task> task)
        {
            tasks.Add(task);
        }

        public int Count => tasks.Count;

        public TaskDispatcher()
        {
            Task.Factory.StartNew(RunSync, TaskCreationOptions.LongRunning);
        }

        private BlockingCollection<Func<Task>> tasks = new BlockingCollection<Func<Task>>();

        private void RunSync()
        {
            foreach(var task in tasks.GetConsumingEnumerable())
            {
                Task.Run(task);
            }
        }
    }

    public class WorkflowScheduler<T> : IDisposable
    {
        private readonly int maxWorkers;
        private readonly CancellationToken cancellationToken;
        private ConcurrentDictionary<string, TaskDispatcher> pendingTasks
            = new ConcurrentDictionary<string, TaskDispatcher>();

        private static List<TaskDispatcher> dispatchers = new List<TaskDispatcher>();

        public WorkflowScheduler(int maxWorkers = 10, CancellationToken cancellationToken = default)
        {
            this.maxWorkers = maxWorkers;
            this.cancellationToken = cancellationToken;
        }



        public void Dispose()
        {
            pendingTasks.Clear();
        }

        internal Task Queue(string id, 
            T item, 
            Func<T, CancellationToken, Task> runWorkflowAsync)
        {
            var q = pendingTasks.GetOrAdd(id, k => { 
                lock(pendingTasks)
                {
                    return pendingTasks.GetOrAdd(k, i =>
                    {
                        lock (dispatchers)
                        {
                            TaskDispatcher d = dispatchers.FirstOrDefault(x => x.Count == 0);
                            if (d == null || dispatchers.Count < maxWorkers)
                            {
                                d = new TaskDispatcher();
                                dispatchers.Add(d);
                                return d;
                            }
                            var f = dispatchers.OrderBy(x => x.Count == 0).First();
                            return f;
                        }
                    });
                }
            });
            var s = new TaskCompletionSource<int>();
            q.Add(async () =>
            {
                await runWorkflowAsync(item, CancellationToken.None);
                s.TrySetResult(1);
            });
            return s.Task;
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
