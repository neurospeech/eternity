using NeuroSpeech.Eternity.Storage;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NeuroSpeech.Eternity
{
    public class WorkflowOptions<T>
    {
        public int Priority { get; set; }

        public string? ID { get; set; }

        public T Input { get; set; }

        public string? Description { get; set; }

        /// <summary>
        /// Leave it empty if you want to start immediately
        /// </summary>
        public DateTimeOffset? ETA { get; set; }
        internal string? ParentID { get; set; }
    }

    /// <summary>
    /// Base class for Eternity Workflow
    /// </summary>
    /// <typeparam name="TWorkflow">Workflow itself</typeparam>
    /// <typeparam name="TInput">Type of input</typeparam>
    /// <typeparam name="TOutput">Type of output</typeparam>
    public abstract class Workflow<TWorkflow,TInput,TOutput>: IWorkflow, IWorkflowObject
        where TWorkflow: Workflow<TWorkflow,TInput,TOutput>
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="context"></param>
        /// <param name="options"></param>
        /// <returns></returns>
        public static Task<string> CreateAsync(EternityContext context, WorkflowOptions<TInput> options)
        {
            context.GetDerived(typeof(TWorkflow));
            return context.CreateAsync(typeof(TWorkflow), options);
        }

        /// <summary>
        /// Creates a new workflow, which will be executed immediately
        /// </summary>
        /// <param name="context">Eternity Context</param>
        /// <param name="input">Input</param>
        /// <returns></returns>
        public static Task<string> CreateAsync(EternityContext context, TInput input, string? description = null)
        {
            // this will force verification..
            context.GetDerived(typeof(TWorkflow));
            return context.CreateAsync(typeof(TWorkflow), new WorkflowOptions<TInput> { 
                Input = input,
                Description = description
            });
        }

        /// <summary>
        /// Executes current workflow within given parent, execution of
        /// parent workflow will be blocked till this workflow is completed
        /// </summary>
        /// <param name="workflow"></param>
        /// <param name="input"></param>
        /// <param name="description"></param>
        /// <returns></returns>
        public static Task<TOutput?> ExecuteAsync(IWorkflowObject workflow, TInput input, string? description = null)
        {
            var w = (workflow as IWorkflow)!;
            return w.Context.ChildAsync<TInput,TOutput?>(
                w,
                typeof(TWorkflow), new WorkflowOptions<TInput> {
                    Input = input,
                    Description = description
                });
        }

        /// <summary>
        /// Retrieve status of the workflow
        /// </summary>
        /// <param name="context"></param>
        /// <param name="id"></param>
        /// <returns>null if workflow not found</returns>
        public static Task<WorkflowStatus<TOutput?>?> GetStatusAsync(EternityContext context, string id)
        {
            return context.GetStatusAsync<TOutput>(id);
        }

        public static WorkflowStatus<TOutput?>? Empty;

        /// <summary>
        /// Retrieve status of the workflow
        /// </summary>
        /// <param name="context"></param>
        /// <param name="id"></param>
        /// <returns>null if workflow not found</returns>
        public static async Task<WorkflowStatus<TOutput?>> WaitForStatusAsync(EternityContext context, string id, int maxWaitMS = 5000)
        {
            var start = 0;
            while(true)
            {
                await Task.Delay(250);
                var result = await GetStatusAsync(context, id);
                if (result == null)
                    return Empty ??= new WorkflowStatus<TOutput?>();
                if (result.Status == EternityEntityState.Completed || result.Status == EternityEntityState.Failed)
                {
                    return result;
                }
                start += 250;
                if (start >= maxWaitMS)
                    return Empty ??= new WorkflowStatus<TOutput?>();
            }
        }

        ///// <summary>
        ///// You can wait till given workflow finishes
        ///// </summary>
        ///// <param name="context"></param>
        ///// <param name="id"></param>
        ///// <returns></returns>
        //public async Task<T?> WaitForFinishAsync<T>(string id, TimeSpan maxWait)
        //{
        //    if (maxWait.TotalSeconds <= 0)
        //    {
        //        throw new ArgumentException($"MaxWait cannot be in the past");
        //    }
        //    var result = await Context.WaitForFinishAsync(this, id, maxWait);
        //    return Context.Deserialize<T?>(result);
        //}

        /// <summary>
        /// Returns if the control is inside an activity
        /// </summary>
        public bool IsActivityRunning { get; internal set; }

        private bool generated;

        bool IWorkflow.IsGenerated => generated;
        
        int IWorkflow.WaitCount { get; set; }

        /// <summary>
        /// Workflow ID associated with current execution
        /// </summary>
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        public string ID { get; private set; }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

        /// <summary>
        /// Set this to true to delete entire history of replay after successful or failed execution.
        /// It is set to true by default, you can turn it off for debugging.
        /// </summary>
        /// <value>True</value>
        public bool DeleteHistory { get; set; } = true;

        /// <summary>
        /// This will preserve the workflow in the storage after it was successfully executed.
        /// Default is 7 days.
        /// </summary>
        public TimeSpan PreserveTime { get; set; } = TimeSpan.FromMinutes(5);

        /// <summary>
        /// This will preserve the workflow in the storge after it was failed.
        /// Default is 30 days.
        /// </summary>
        public TimeSpan FailurePreserveTime { get; set; } = TimeSpan.FromDays(1);

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        public EternityContext Context { get; private set; }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

        /// <summary>
        /// Time associated with current execution, it will not be same as current date time when it is replayed
        /// </summary>
        public DateTimeOffset CurrentUtc { get; private set; }

        Type IWorkflow.InputType => typeof(TInput);

        bool IWorkflow.IsActivityRunning { get => IsActivityRunning; set => IsActivityRunning = value; }

        IList<string> IWorkflow.QueueItemList { get; } = new List<string>();

        private EternityEntity _entity;
        EternityEntity IWorkflow.Entity { get => _entity; set => _entity = value; }

        public int Priority { get => _entity.Priority; set => _entity.Priority = value; }

        public IDictionary<string,string> Extra { get => _entity.ExtraDictionary; }

        public Task SaveAsync()
        {
            return Context.SaveAsync(this);
        }

        public abstract Task<TOutput> RunAsync(TInput input);

        void IWorkflow.Init(string id, EternityContext context, DateTimeOffset start, bool generated)
        {
            this.ID = id;
            this.Context = context;
            this.CurrentUtc = start;
            this.generated = generated;
        }

        /// <summary>
        /// Wait for an external event upto given timespan, timespan cannot be infinite, and cannot be zero or negative
        /// </summary>
        /// <param name="maxWait"></param>
        /// <param name="names">Names of expected events</param>
        /// <returns></returns>
        public Task<(string? name, string? value)> WaitForExternalEventsAsync(TimeSpan maxWait,params string[] names)
        {
            if (IsActivityRunning)
            {
                throw new InvalidOperationException($"Cannot wait for an event inside an activity");
            }
            if (maxWait.TotalMilliseconds <= 0)
            {
                throw new ArgumentException($"{nameof(maxWait)} cannot be in the past");
            }
            if(names.Length == 0)
            {
                throw new ArgumentException($"{nameof(names)} cannot be empty");
            }
            return Context.WaitForExternalEventsAsync(this, names, CurrentUtc.Add(maxWait));
        }

        /// <summary>
        /// Pause the execution for given time span, it cannot be zero or negative and it cannot be infinite
        /// </summary>
        /// <param name="timeout"></param>
        /// <returns></returns>
        public Task Delay(TimeSpan timeout)
        {
            if (timeout.TotalMilliseconds <= 0)
            {
                throw new ArgumentException($"{nameof(timeout)} cannot be in the past");
            }
            return Context.Delay(this, ID, CurrentUtc.Add(timeout));
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        public Task<T> InternalScheduleResultAsync<T>(string method, params object?[] items)
        {
            var fx = typeof(TWorkflow).GetVirtualMethod(method);
            var unique = fx.GetCustomAttribute<ActivityAttribute>();
            return Context.ScheduleAsync<T>(this, unique.UniqueParameters, ID, CurrentUtc, fx, items);
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        public async Task InternalScheduleAsync(string method, params object?[] items)
        {
            var fx = typeof(TWorkflow).GetVirtualMethod(method);
            var unique = fx.GetCustomAttribute<ActivityAttribute>();
            await Context.ScheduleAsync<object>(this, unique.UniqueParameters, ID, CurrentUtc, fx, items);
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        public Task<T> InternalScheduleAtResultAsync<T>(DateTimeOffset at, string method, params object?[] items)
        {
            if (at <= CurrentUtc)
            {
                throw new ArgumentException($"{nameof(at)} cannot be in the past");
            }
            var fx = typeof(TWorkflow).GetVirtualMethod(method);
            var unique = fx.GetCustomAttribute<ActivityAttribute>();
            return Context.ScheduleAsync<T>(this, unique.UniqueParameters, ID, at, fx, items);
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        public async Task InternalScheduleAtAsync(DateTimeOffset at, string method, params object?[] items)
        {
            if (at <= CurrentUtc)
            {
                throw new ArgumentException($"{nameof(at)} cannot be in the past");
            }
            var fx = typeof(TWorkflow).GetVirtualMethod(method);
            var unique = fx.GetCustomAttribute<ActivityAttribute>();
            await Context.ScheduleAsync<object>(this, unique.UniqueParameters, ID, at, fx, items);
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        public Task<T> InternalScheduleAfterResultAsync<T>(TimeSpan at, string method, params object?[] items)
        {
            if (at.TotalMilliseconds <= 0)
            {
                throw new ArgumentException($"{nameof(at)} cannot be in the past");
            }
            var fx = typeof(TWorkflow).GetVirtualMethod(method);
            var unique = fx.GetCustomAttribute<ActivityAttribute>();
            return Context.ScheduleAsync<T>(this, unique.UniqueParameters, ID, CurrentUtc.Add(at), fx, items);
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        public async Task InternalScheduleAfterAsync(TimeSpan at, string method, params object?[] items)
        {
            if (at.TotalMilliseconds <= 0)
            {
                throw new ArgumentException($"{nameof(at)} cannot be in the past");
            }
            var fx = typeof(TWorkflow).GetVirtualMethod(method);
            var unique = fx.GetCustomAttribute<ActivityAttribute>();
            await Context.ScheduleAsync<object>(this, unique.UniqueParameters, ID, CurrentUtc.Add(at), fx, items);
        }


        void IWorkflow.SetCurrentTime(DateTimeOffset time)
        {
            this.CurrentUtc = time;
        }

        async Task<object> IWorkflow.RunAsync(object input)
        {
            object? result = null;
            bool suspended = false;
            try
            {
                result = await RunAsync((TInput)input);
            } 
            catch(ActivitySuspendedException)
            {
                suspended = true;
                throw;
            }catch(Exception)
            {
                throw;
            }
            finally
            {
                if(!suspended)
                {
                    await RunFinallyAsync();
                }
            }
            return result!;
        }

        protected virtual Task RunFinallyAsync()
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// Use this to fan-out as it will handle Suspended activities correctly
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="tasks"></param>
        /// <returns></returns>
        protected async Task<IReadOnlyList<T?>> WhenAll<T>(IEnumerable<Task<T?>> tasks)
        {
            var final = new List<Task<(T? result,Exception? exception)>>();
            foreach(var item in tasks)
            {
                final.Add(AwaitAsync(item));
            }
            var intermediateResults = await Task.WhenAll(final);

            var errors = new StringBuilder();

            var results = new List<T?>();

            foreach(var r in intermediateResults)
            {
                if (r.exception != null)
                {
                    // we need to await fan in 
                    // till all tasks are completed
                    // finished or failed...
                    if (r.exception is ActivitySuspendedException)
                        throw r.exception;
                    errors.AppendLine(r.exception.ToString());
                    continue;
                }
                results.Add(r.result);
            }

            if (errors.Length > 0) {
                throw new ActivityFailedException(errors.ToString());
            }

            return results;

            static async Task<(T?, Exception?)> AwaitAsync(Task<T?> item)
            {
                try
                {
                    return (await item, null);
                } catch (Exception ex)
                {
                    return (default, ex);
                }
            }
        }
    }
}
