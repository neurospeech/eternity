using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NeuroSpeech.Eternity
{
    public sealed class SingleThreadTaskScheduler<T> : TaskScheduler
    {
        [ThreadStatic]
        private static bool isExecuting;
        private readonly CancellationToken cancellationToken;

        private readonly BlockingCollection<Task> taskQueue;

        public TaskFactory<T> Factory { get; }

        public int Count => taskQueue.Count;

        public SingleThreadTaskScheduler(CancellationToken cancellationToken = default)
        {
            this.cancellationToken = cancellationToken;
            this.taskQueue = new BlockingCollection<Task>();
            this.Factory = new TaskFactory<T>(this);
        }

        public void Start()
        {
            new Thread(RunOnCurrentThread) { Name = "STTS Thread" }.Start();
        }

        // Just a helper for the sample code
        public Task Schedule(Action action)
        {
            return
                Task.Factory.StartNew
                    (
                        action,
                        CancellationToken.None,
                        TaskCreationOptions.None,
                        this
                    );
        }

        // You can have this public if you want - just make sure to hide it
        private void RunOnCurrentThread()
        {
            isExecuting = true;

            try
            {
                foreach (var task in taskQueue.GetConsumingEnumerable(cancellationToken))
                {
                    TryExecuteTask(task);
                }
            }
            catch (OperationCanceledException)
            { }
            finally
            {
                isExecuting = false;
            }
        }

        // Signaling this allows the task scheduler to finish after all tasks complete
        public void Complete() { taskQueue.CompleteAdding(); }
        protected override IEnumerable<Task> GetScheduledTasks() { return null; }

        protected override void QueueTask(Task task)
        {
            try
            {
                taskQueue.Add(task, cancellationToken);
            }
            catch (OperationCanceledException)
            { }
        }

        protected override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued)
        {
            // We'd need to remove the task from queue if it was already queued. 
            // That would be too hard.
            if (taskWasPreviouslyQueued) return false;

            return isExecuting && TryExecuteTask(task);
        }
    }
}
