using NeuroSpeech.Eternity.Converters;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace NeuroSpeech.Eternity
{

    public class EternityContext
    {
        private readonly IEternityStorage storage;
        private readonly IServiceProvider services;
        private readonly IEternityClock clock;
        private readonly IServiceScopeFactory? scopeFactory;
        private readonly System.Text.Json.JsonSerializerOptions options;
        private CancellationTokenSource? waiter;
        private IEternityLogger? logger;

        /// <summary>
        /// Please turn on EmitAvailable on iOS
        /// </summary>
        public bool EmitAvailable { get; set; } = true;
        


        public EternityContext(
            IEternityStorage storage, 
            IServiceProvider services,
            IEternityClock clock,
            IEternityLogger? logger = null)
        {
            this.logger = logger;
            this.storage = storage;
            this.services = services;
            this.clock = clock;
            this.scopeFactory = services?.GetService(typeof(IServiceScopeFactory)) as IServiceScopeFactory;
            this.options = new JsonSerializerOptions()
            {
                AllowTrailingCommas = true,
                IgnoreReadOnlyProperties = true,
                IgnoreReadOnlyFields = true,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
                Converters = { 
                    new ValueTupleConverter()
                }
            };
        }

        private static ConcurrentDictionary<Type, Type> generatedTypes = new ConcurrentDictionary<Type, Type>();

        internal Type GetDerived(Type type)
        {
            return generatedTypes.GetOrAdd(type, t =>
            {
                lock (generatedTypes)
                {
                    return generatedTypes.GetOrAdd(t, k =>
                    {
                        var generatedType = type.Assembly.GetExportedTypes()
                            .FirstOrDefault(x => type.IsAssignableFrom(x) && x.GetCustomAttribute<GeneratedWorkflowAttribute>() != null);

                        if (generatedType != null)
                            return generatedType;

                        // check if we are in iOS
                        if (this.EmitAvailable)
                            return ClrHelper.Instance.Create(type);
                        // if generated code is available... in the same assembly..
                        return type;
                    });
                }
            });
        }

        private void Trigger()
        {
            var w = waiter;
            if (w != null && !w.IsCancellationRequested)
            {
                w.Cancel();
            }
        }

        public event EventHandler? NewWorkflowCreated;

        internal async Task<string> CreateAsync<TInput, TOutput>(Type type, WorkflowOptions<TInput> input)
        {
            input.ID ??= Guid.NewGuid().ToString("N");
            var utcNow = clock.UtcNow;
            var eta = input.ETA ?? utcNow;
            var key = WorkflowStep.Workflow(input.ID, type, input.Input!, input.Description, eta, utcNow, input.ParentID, options);
            key = await storage.InsertWorkflowAsync(key);
            await storage.QueueWorkflowAsync(new WorkflowQueueItem { ID = key.ID!, ETA = utcNow });
            NewWorkflowCreated?.Invoke(this, EventArgs.Empty);
            Trigger();
            return input.ID;
        }

        internal async Task<WorkflowStatus<T?>?> GetStatusAsync<T>(string id)
        {
            var wf = await storage.GetWorkflowAsync(id);
            if (wf == null)
                return null;
            var status = new WorkflowStatus<T?>
            {
                Status = wf.Status,
                DateCreated = wf.DateCreated,
                LastUpdate = wf.LastUpdated
            };
            switch (wf.Status)
            {
                case ActivityStatus.Completed:
                    status.Result = Deserialize<T?>(wf.Result);
                    break;
                case ActivityStatus.Failed:
                    status.Error = wf.Error;
                    break;
            }
            return status;
        }

        public async Task ProcessChunkedMessagesAsync(
            int maxActivitiesToProcess = 10000,
            int chunkSize = 100,
            TimeSpan pollingGap = default,
            CancellationToken cancellationToken = default)
        {
            if (pollingGap == TimeSpan.Zero)
            {
                pollingGap = TimeSpan.FromMinutes(5);
            }
            while (!cancellationToken.IsCancellationRequested)
            {
                var items = await storage.GetScheduledActivitiesAsync(chunkSize);
                if (items.Length > 0)
                {
                    using var ws = new WorkflowQueue(cancellationToken);
                    var pending = maxActivitiesToProcess;
                    while (pending > 0)
                    {
                        int remaining = await ws.QueueAny(items, RunWorkflowAsync);

                        if(remaining > chunkSize)
                        {
                            await ws.WaitForAll();
                        }
                        else
                        {
                            ws.ClearCompleted();
                        }

                        pending -= items.Length;
                        items = await storage.GetScheduledActivitiesAsync(chunkSize);
                        if (items.Length == 0)
                            break;
                    }
                    await ws.WaitForAll();
                    continue;
                }
                try
                {
                    var c = new CancellationTokenSource();
                    waiter = c;
                    await Task.Delay(pollingGap, c.Token);
                }
                catch (TaskCanceledException)
                {

                }
            }
        }

        public async Task ProcessMessagesAsync(
            int maxActivitiesToProcess = 100, 
            TimeSpan pollingGap = default,
            CancellationToken cancellationToken = default)
        {
            if(pollingGap == TimeSpan.Zero)
            {
                pollingGap = TimeSpan.FromMinutes(5);
            }
            while(!cancellationToken.IsCancellationRequested)
            {
                var items = await storage.GetScheduledActivitiesAsync(maxActivitiesToProcess);
                if (items.Length > 0)
                {
                    using var ws = new WorkflowScheduler<WorkflowQueueItem>(cancellationToken);
                    var tasks = new Task[items.Length];
                    for (int i = 0; i < items.Length; i++)
                    {
                        var item = items[i];
                        tasks[i] = ws.Queue(item.ID, item, RunWorkflowAsync);
                    }
                    await Task.WhenAll(tasks);
                    continue;
                }
                try
                {
                    var c = new CancellationTokenSource();
                    waiter = c;
                    await Task.Delay(pollingGap, c.Token);
                } catch (TaskCanceledException)
                {

                }
            }
        }

        private Task<int>? previousTask = null;

        public Task<int> ProcessMessagesOnceAsync(int maxActivitiesToProcess = 100, CancellationToken cancellationToken = default) {
            lock (this)
            {
                previousTask = InternalProcessMessagesOnceAsync(previousTask, maxActivitiesToProcess, cancellationToken);
                return previousTask;
            }
        }

        private async Task<int> InternalProcessMessagesOnceAsync(
            Task<int>? previous,
            int maxActivitiesToProcess = 100, 
            CancellationToken cancellationToken = default)
        {
            if(previous != null)
            {
                await previous;
            }
            var items = await storage.GetScheduledActivitiesAsync(maxActivitiesToProcess);
            if (items.Length == 0)
                return items.Length;
            using var ws = new WorkflowScheduler<WorkflowQueueItem>(cancellationToken);
            var tasks = new Task[items.Length];
            for (int i = 0; i < items.Length; i++)
            {
                var item = items[i];
                tasks[i] = ws.Queue(item.ID, item, RunWorkflowAsync);
            }
            await Task.WhenAll(tasks);
            return items.Length;
        }

        private async Task RunWorkflowAsync(WorkflowQueueItem queueItem, CancellationToken cancellation = default)
        {
            using var session = this.logger.BeginLogSession();
            if(queueItem.Command == "Delete")
            {
                await storage.DeleteWorkflowAsync(queueItem.ID);
                await storage.RemoveQueueAsync(queueItem.QueueToken);
                session?.LogInformation($"Workflow {queueItem.ID} deleted");
                return;
            }

            var step = await storage.GetWorkflowAsync(queueItem.ID);
            if (step==null || step.Status == ActivityStatus.Completed || step.Status == ActivityStatus.Failed)
            {
                await storage.RemoveQueueAsync(queueItem.QueueToken);
                session?.LogInformation($"Workflow {queueItem.ID} finished. Status = {step?.Status}");
                return;
            }
            var originalType = Type.GetType(step.WorkflowType);
            var workflowType = this.GetDerived(originalType);
            // we need to begin...
            var instance = GetWorkflowInstance(workflowType, step.ID!, step.LastUpdated);
            instance.QueueItemList.Add(queueItem.QueueToken);
            DateTimeOffset deleteOn;
            try
            {
                var input = JsonSerializer.Deserialize(step.Parameter!, instance.InputType, options);
                var result = await instance.RunAsync(input!);
                step.Result = JsonSerializer.Serialize(result, options);
                step.LastUpdated = clock.UtcNow;
                step.Status = ActivityStatus.Completed;
                deleteOn = clock.UtcNow.Add(instance.PreserveTime);
                session?.LogInformation($"Workflow {step.ID} completed.");
            }
            catch (ActivitySuspendedException)
            {
                step.Status = ActivityStatus.Suspended;
                await storage.UpdateAsync(step);
                await storage.RemoveQueueAsync(queueItem.QueueToken);
                session?.LogInformation($"Workflow {step.ID} suspended.");
                return;
            }
            catch (Exception ex)
            {
                step.Error = ex.ToString();
                step.Status = ActivityStatus.Failed;
                step.LastUpdated = clock.UtcNow;
                deleteOn = clock.UtcNow.Add(instance.PreserveTime);
                session?.LogInformation($"Workflow {step.ID} failed. {ex.ToString()}");
            }
            if (step.ParentID != null)
            {
                await RaiseEventAsync(step.ParentID, step.ID!, "Success");
                session?.LogInformation($"Workflow {step.ID} Raised Event for Parent {step.ParentID}");
            }
            await storage.UpdateAsync(step);
            await storage.RemoveQueueAsync(instance.QueueItemList.ToArray());

            await storage.QueueWorkflowAsync(new WorkflowQueueItem { ID = instance.ID, ETA = deleteOn, Command = "Delete" });

            if (instance.DeleteHistory)
            {
                try
                {
                    await storage.DeleteHistoryAsync(step.ID!);
                }catch (Exception)
                {
                    // ignore error...
                }
            }
        }

        //internal async Task<string?> WaitForFinishAsync(IWorkflow workflow, string id, TimeSpan maxWait)
        //{
        //    var current = workflow.CurrentUtc.Add(maxWait);
        //    var completed = $"completed-{id}";
        //    var failed = $"failed-{id}";
        //    await WaitForExternalEventsAsync(workflow, new string[] { completed, failed }, current);
        //    var s = await storage.GetWorkflowAsync(id);
        //    switch (s.Status)
        //    {
        //        case ActivityStatus.Completed:
        //            return s.Result;
        //        case ActivityStatus.Failed:
        //            throw new ActivityFailedException(s.Error!);
        //    }
        //    throw new TimeoutException();
        //}

        internal async Task<TOutput?> ChildAsync<TInput, TOutput>(IWorkflow workflow, Type childType, WorkflowOptions<TInput> input)
        {
            var utcNow = clock.UtcNow;
            input.ETA ??= utcNow;
            var key = ActivityStep.Child(workflow.ID, childType, input.Input!, input.ETA.Value, utcNow, options);
            var status = await GetActivityResultAsync(workflow, key, async t => {
                input.ParentID = workflow.ID;
                t.Result = await this.CreateAsync<TInput, TOutput>(childType, input);
                t.Status = ActivityStatus.Completed;
                await storage.UpdateAsync(t);
                return t;
            });

            // now wait for event..
            var r = await this.WaitForExternalEventsAsync(workflow, new string[] { status.Result! }, utcNow.AddDays(1));
            if(r.name == status.Result)
            {
                var ws = await storage.GetWorkflowAsync(r.name!);
                if(ws.Status == ActivityStatus.Failed)
                {
                    throw new ActivityFailedException(ws.Error!);
                }
                return ws.AsResult<TOutput?>(options);
            }
            throw new TimeoutException();
        }

        internal async Task Delay(IWorkflow workflow, string id, DateTimeOffset timeout)
        {
            
            var key = ActivityStep.Delay(id, timeout, workflow.CurrentUtc);
            var status = await GetActivityResultAsync(workflow, key);

            switch (status.Status)
            {
                case ActivityStatus.Completed:
                    workflow.SetCurrentTime(status.LastUpdated);
                    return;
                case ActivityStatus.Failed:
                    workflow.SetCurrentTime(status.LastUpdated);
                    throw new ActivityFailedException(status.Error!);
            }

            var utcNow = clock.UtcNow;
            if (status.ETA <= utcNow)
            {
                // this was in the past...
                status.Status = ActivityStatus.Completed;
                status.Result = "null";
                status.LastUpdated = utcNow;
                await storage.UpdateAsync(status);
                if(status.QueueToken != null)
                {
                    await storage.RemoveQueueAsync(status.QueueToken);
                }
                return;
            }

            var diff = status.ETA - utcNow;
            if (diff.TotalSeconds > 15)
            {
                throw new ActivitySuspendedException();
            }

            await Task.Delay(diff);

            status.Status = ActivityStatus.Completed;
            status.Result = "null";
            status.LastUpdated = clock.UtcNow;
            await storage.UpdateAsync(status);
            workflow.SetCurrentTime(status.LastUpdated);
            if(status.QueueToken != null)
                await storage.RemoveQueueAsync(status.QueueToken);
        }

        public T ResolveSingleton<T>()
        {
            return this.services.GetService(typeof(T)) is T service
                ? service
                : throw new ArgumentException($"Cannot resolve service {typeof(T).FullName}");
        }
        
        public IEternityServiceScope CreateScope()
        {
            return this.scopeFactory?.CreateScope(services) ?? throw new ArgumentException($"ScopeFactory is not set");
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="id"></param>
        /// <param name="eventName"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public async Task RaiseEventAsync(
            string id,
            string eventName,
            string? value,
            bool throwIfNotFound = false)
        {
            using var session = this.logger.BeginLogSession();
            session?.LogInformation($"Workflow {id} Raising Event {eventName}");
            value ??= "";
            var key = await storage.GetEventAsync(id, eventName);
            if (key == null)
            {
                session?.LogError($"Workflow {id} is not waiting for event {eventName}");
                if(throwIfNotFound)
                    throw new NotSupportedException();
                return;
            }
            if(key.Status == ActivityStatus.Completed || key.Status == ActivityStatus.Failed)
            {
                session?.LogInformation($"Workflow {id} eventName {eventName} is already {key.Status}");
                return;
            }
            key.Result = Serialize(new EventResult {
                EventName = eventName,
                Value = value
            });
            key.ETA = clock.UtcNow;
            key.Status = ActivityStatus.Completed;
            // we need to change queue token here...
            key.QueueToken = await storage.QueueWorkflowAsync(new WorkflowQueueItem { ID = key.ID!, ETA = key.ETA }, key.QueueToken);
            await storage.UpdateAsync(key);
            if (waitingTokens.TryGetValue(key.Key!, out var ct)) {
                if (!ct.IsCancellationRequested)
                {
                    try
                    {
                        ct.Cancel();
                    }
                    catch { }
                }
            }
            Trigger();
            session?.LogInformation($"Workflow {id} Queued successfully.");
        }

        private static ConcurrentDictionary<string, CancellationTokenSource> waitingTokens = new ConcurrentDictionary<string, CancellationTokenSource>();

        internal async Task<(string? name, string? value)> WaitForExternalEventsAsync(
            IWorkflow workflow, 
            string[] names, 
            DateTimeOffset eta)
        {
            using var session = this.logger.BeginLogSession();
            session?.LogInformation($"Workflow {workflow.ID} waiting for an external event");
            var key = ActivityStep.Event(workflow.ID, names, eta, workflow.CurrentUtc);

            var status = await GetActivityResultAsync(workflow, key);

            while (true)
            {

                switch (status.Status)
                {
                    case ActivityStatus.Completed:
                        workflow.SetCurrentTime(status.LastUpdated);
                        var er = status.AsResult<EventResult>(options)!;
                        session?.LogInformation($"Workflow {workflow.ID} waiting for an external event finished {status.Result}");
                        return (er.EventName, er.Value);
                    case ActivityStatus.Failed:
                        workflow.SetCurrentTime(status.LastUpdated);
                        session?.LogInformation($"Workflow {workflow.ID} waiting for an external event failed {status.Error}");
                        throw new ActivityFailedException(status.Error!);
                }

                var diff = status.ETA - clock.UtcNow;
                if (diff.TotalSeconds > 15)
                {
                    throw new ActivitySuspendedException();
                }

                if (diff.TotalMilliseconds > 0)
                {
                    var tokenKey = $"{key.Key}";
                    var token = waitingTokens.AddOrUpdate(tokenKey,
                        (x) => new CancellationTokenSource(),
                        (k, update) => new CancellationTokenSource());
                    try
                    {
                        await Task.Delay(diff, token.Token);
                    }
                    catch (TaskCanceledException) { }
                }

                status = await GetActivityResultAsync(workflow, status);
                if(status.Status != ActivityStatus.Completed && status.Status != ActivityStatus.Failed)
                {
                    var timedout = new EventResult { };
                    status.Result = Serialize(timedout);
                    status.Status = ActivityStatus.Completed;
                    status.LastUpdated = clock.UtcNow;
                    workflow.SetCurrentTime(status.LastUpdated);
                    await storage.UpdateAsync(status);
                    if(status.QueueToken != null)
                        await storage.RemoveQueueAsync(status.QueueToken);
                    return (null, null);
                }
            }
        }

        internal async Task<ActivityStep> GetActivityResultAsync(
            IWorkflow workflow, 
            ActivityStep key,
            Func<ActivityStep,Task<ActivityStep>>? onCreate = null)
        {
            var r = await storage.GetStatusAsync(key);
            if (r != null){
                return r;
            }
            key = await storage.InsertActivityAsync(key);
            var qi = await storage.QueueWorkflowAsync(new WorkflowQueueItem { ID = key.ID!, ETA = key.ETA });
            key.QueueToken = qi;
            workflow.QueueItemList.Add(qi);
            if (onCreate != null)
            {
                key = await onCreate(key);
            }
            return key;
        }

        public TActivityOutput? Deserialize<TActivityOutput>(string? result)
        {
            return JsonSerializer.Deserialize<TActivityOutput>(result!);
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        internal async Task<TActivityOutput> ScheduleAsync<TActivityOutput>(
            IWorkflow workflow,
            bool uniqueParameters,
            string ID,
            DateTimeOffset after,
            MethodInfo method,
            params object?[] input)
        {

            string methodName = method.Name;

            if (workflow.IsActivityRunning)
            {
                throw new InvalidOperationException($"Cannot schedule an activity inside an activity");
            }
            using var session = this.logger?.BeginLogSession();

            var key = ActivityStep.Activity(uniqueParameters, ID, method, input, after, workflow.CurrentUtc, options);

            while (true)
            {

                // has result...
                var task = await GetActivityResultAsync(workflow, key);

                switch (task.Status)
                {
                    case ActivityStatus.Failed:
                        workflow.SetCurrentTime(task.LastUpdated);
                        throw new ActivityFailedException(task.Error!);
                    case ActivityStatus.Completed:
                        workflow.SetCurrentTime(task.LastUpdated);
                        if (typeof(TActivityOutput) == typeof(object))
                            return (TActivityOutput)(object)"null";
                        return task.AsResult<TActivityOutput>(options)!;
                }

                session?.LogInformation($"Workflow {ID} Scheduling new activity {methodName}");
                var diff = task.ETA - clock.UtcNow;
                if(diff.TotalSeconds > 15)
                {
                    throw new ActivitySuspendedException();
                }

                await RunActivityAsync(workflow, task);
            }


        }

        internal async Task RunActivityAsync(IWorkflow workflow, ActivityStep key)
        {
            using var session = this.logger.BeginLogSession();

            session?.LogInformation($"Wrokflow {workflow.ID} executing activity {key.Method}");

            var task = await GetActivityResultAsync(workflow, key);

            var sequenceId = task.SequenceID;

            var type = this.EmitAvailable ? workflow.GetType().BaseType : workflow.GetType();

            // we are supposed to run this activity now...
            // acquire execution lock...
            var executionLock = await storage.AcquireLockAsync(key.ID!, sequenceId);
            try
            {

                // requery that status...
                task = await GetActivityResultAsync(workflow, key);
                switch (task.Status)
                {
                    case ActivityStatus.Completed:
                        workflow.SetCurrentTime(key.LastUpdated);
                        session?.LogInformation($"Wrokflow {workflow.ID} executing activity finished");
                        return;
                    case ActivityStatus.Failed:
                        workflow.SetCurrentTime(key.LastUpdated);
                        session?.LogInformation($"Wrokflow {workflow.ID} executing activity failed {task.Error}");
                        throw new ActivityFailedException(key.Error!);
                }

                using var scope = scopeFactory?.CreateScope(services);

                try
                {
                    session?.LogInformation($"Wrokflow {workflow.ID} running activity {key.Method}");
                    workflow.IsActivityRunning = true;
                    var method = type.GetMethod(key.Method);

                    var parameters = BuildParameters(method, key.Parameters, scope?.ServiceProvider ?? services);

                    // if type is generated...
                    var result = (workflow.IsGenerated || !EmitAvailable)
                        ? await method.InvokeAsync(workflow, parameters, options)
                        : await method.RunAsync(workflow, parameters, options);
                    key.Result = result;
                    key.Status = ActivityStatus.Completed;
                    key.LastUpdated = clock.UtcNow;
                    workflow.SetCurrentTime(key.LastUpdated);
                    await storage.UpdateAsync(key);
                    session?.LogInformation($"Wrokflow {workflow.ID} executing activity finished");
                    return;

                }
                catch (Exception ex) when (!(ex is ActivitySuspendedException))
                {
                    key.Error = ex.ToString();
                    key.Status = ActivityStatus.Failed;
                    key.LastUpdated = clock.UtcNow;
                    await storage.UpdateAsync(key);
                    workflow.SetCurrentTime(key.LastUpdated);
                    session?.LogInformation($"Wrokflow {workflow.ID} executing activity failed {ex.ToString()}");
                    throw new ActivityFailedException(ex.ToString());
                } finally
                {
                    workflow.IsActivityRunning = false;
                }


                // record the result here as well..
            }
            finally
            {
                await storage.FreeLockAsync(executionLock);
            }
        }

        private object?[] BuildParameters(MethodInfo method, string[]? parameters, IServiceProvider? serviceProvider)
        {
            var pas = method.GetParameters();
            var result = new object?[pas.Length];
            for (int i = 0; i < pas.Length; i++)
            {
                var pa = pas[i];
                if(pa.GetCustomAttribute<InjectAttribute>() == null)
                {
                    var value = parameters![i];
                    result[i] = JsonSerializer.Deserialize(value!, pa.ParameterType, options);
                    continue;
                }
                if (serviceProvider == null)
                    throw new ArgumentNullException($"{nameof(serviceProvider)} is null");
                var serviceRequested = serviceProvider.GetService(pa.ParameterType)
                    ?? throw new ArgumentException($"No service registered for {pa.ParameterType.FullName}");
                result[i] = serviceRequested;
            }
            return result;
        }

        private IWorkflow GetWorkflowInstance(Type type, string id, DateTimeOffset eta)
        {
            var w = (Activator.CreateInstance(type) as IWorkflow)!;
            w.Init(id, this, eta, type.GetCustomAttribute<GeneratedWorkflowAttribute>() != null );
            return w;
        }

        public string Serialize<TActivityOutput>(TActivityOutput result)
        {
            return JsonSerializer.Serialize(result);
        }
    }

}
