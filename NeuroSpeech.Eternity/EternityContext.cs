using NeuroSpeech.Eternity.Converters;
using NeuroSpeech.Eternity.Storage;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace NeuroSpeech.Eternity
{

    public class EternityContext
    {
        internal readonly IServiceProvider services;
        private readonly IEternityClock clock;
        private readonly IEternityRepository repository;
        internal readonly IEternityLogger? logger;
        private readonly IServiceScopeFactory? scopeFactory;

        public event EventHandler? NewWorkflow;

        private WaitingTokens waitingTokens;
        private readonly JsonSerializerOptions options;

        private Waiter waiter = new Waiter();

        /// <summary>
        /// Please turn off EmitAvailable on iOS
        /// </summary>
        public bool EmitAvailable { get; set; } = true;

        public CancellationToken Cancellation { get; private set; }

        private void SetMaxPollingGap(TimeSpan ts)
        {
            if (ts.TotalMinutes > 5)
                return;
            if (ts.TotalMilliseconds <= 0)
            {
                Trigger();
                return;
            }
            //waiter.ClearAfter(ts);
            Task.Run(async () =>
            {
                await Task.Delay(ts);
                Trigger();
            });
        }

        public EternityContext(
            IServiceProvider services,
            IEternityClock clock,
            IEternityRepository repository,
            IEternityLogger? logger = null)
        {
            this.services = services;
            this.clock = clock;
            this.repository = repository;
            this.logger = logger;
            this.scopeFactory = services.GetService(typeof(IServiceScopeFactory)) as IServiceScopeFactory;
            this.waitingTokens = new WaitingTokens(1);
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

        internal async Task<string> CreateAsync<TInput>(Type type, WorkflowOptions<TInput> options, bool throwIfExists = true)
        {
            var id = options.ID ?? Guid.NewGuid().ToString("N");
            var now = clock.UtcNow;
            var eta = options.ETA ?? now;
            var entity = new EternityEntity(id, type.AssemblyQualifiedName, Serialize(options.Input));
            entity.UtcETA = eta;
            entity.UtcCreated = now;
            entity.UtcUpdated = entity.UtcCreated;
            entity.Priority = options.Priority;
            var existing = await repository.GetAsync(id);
            if (existing != null)
            {
                if (throwIfExists) {
                    throw new ArgumentException($"Workflow already exists");
                }
                return id;
            }
            await repository.SaveAsync(entity);
            NewWorkflow?.Invoke(this, EventArgs.Empty);
            logger?.Log(System.Diagnostics.TraceEventType.Information, $"New workflow created {id}");
            var diff = eta - now;
            SetMaxPollingGap(diff);
            return id;
        }

        public void Trigger(string? id = null)
        {
            if (id != null)
            {
                // something wrong...
                waitingTokens.Cancel(id);
            }
            waiter.Clear();
        }

        internal async Task<WorkflowStatus<T?>?> GetStatusAsync<T>(string id)
        {
            var result = await repository.GetAsync(id);
            if (result == null)
            {
                return null;
            }
            var status = new WorkflowStatus<T?> { 
                Status = result.State,
                DateCreated = result.UtcCreated,
                LastUpdate = result.UtcUpdated,
                Extra = result.ExtraDictionary,
            };
            switch (result.State)
            {
                case EternityEntityState.Completed:
                    status.Result = Deserialize<T?>(result.Response);
                    break;
                case EternityEntityState.Failed:
                    status.Error = result.Response;
                    break;
            }
            return status;
        }

        public async Task ProcessMessagesAsync(
            int maxActivitiesToProcess = 100,
            TimeSpan pollingGap = default,
            CancellationToken cancellationToken = default)
        {
            this.Cancellation = cancellationToken;
            if (pollingGap == TimeSpan.Zero)
            {
                pollingGap = TimeSpan.FromMinutes(5);
            }
            while (!cancellationToken.IsCancellationRequested)
            {
                var items = await repository.DequeueAsync(maxActivitiesToProcess, clock.UtcNow);
                if (items.Count > 0)
                {
                    var list = new Task[items.Count];
                    int index = 0;
                    foreach(var item in items)
                    {
                        list[index++] = Task.Run(() => RunWorkflowAsync(item, cancellationToken), cancellationToken);
                    }
                    await Task.WhenAll(list);
                    continue;
                }
                await waiter.WaitAsync(pollingGap, cancellationToken);
                waitingTokens.Clear();
            }
        }

        private Task<int>? previousTask = null;

        public Task<int> ProcessMessagesOnceAsync(int maxActivitiesToProcess = 100, CancellationToken cancellationToken = default)
        {
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
            if (previous != null)
            {
                await previous;
            }
            var items = await repository.DequeueAsync(maxActivitiesToProcess, clock.UtcNow);
            if (items.Count == 0)
                return items.Count;
            var tasks = new Task[items.Count];
            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                tasks[i] = Task.Run(() => RunWorkflowAsync(item, cancellationToken), cancellationToken);
            }
            await Task.WhenAll(tasks);
            waitingTokens.Clear();
            return items.Count;
        }

        private static TimeSpan MaxLock = TimeSpan.FromMinutes(1);

        private IWorkflow GetWorkflowInstance(EternityEntity entity, Type originalType, Type type, string id, DateTimeOffset eta)
        {
            var w = (Activator.CreateInstance(type) as IWorkflow)!;
            w.Init(
                id,
                this,
                eta,
                type.GetCustomAttribute<GeneratedWorkflowAttribute>() != null,
                originalType);
            w.Entity = entity;
            return w;
        }

        internal Task SaveAsync(IWorkflow workflow)
        {
            return repository.SaveAsync(workflow.Entity);
        }

        public T ResolveSingleton<T>()
        {
            var service = this.services.GetService(typeof(T));
            if (service is not T serviceInstance)
                throw new ArgumentException($"No service registered for {typeof(T).FullName}");
            return serviceInstance;
        }

        private async Task RunWorkflowAsync(EternityEntity entity, CancellationToken arg2)
        {
            using var session = this.logger.BeginLogSession();
            try
            {
                if (entity.State == EternityEntityState.Completed
                    || entity.State == EternityEntityState.Failed)
                {
                    if (entity.UtcETA <= clock.UtcNow)
                    {
                        // time to delete...
                        await repository.DeleteAsync(entity);
                    }
                    return;
                }

                IWorkflow? instance = null;

                try
                {
                    var originalType = Type.GetType(entity.Name);
                    var workflowType = this.GetDerived(originalType);
                    instance = GetWorkflowInstance(entity, originalType, workflowType, entity.ID, entity.UtcCreated);
                    var input = JsonSerializer.Deserialize(entity.Input ?? "null", instance.InputType, options);
                    var result = await instance.RunAsync(input!);
                    var now = clock.UtcNow;
                    entity.Response = JsonSerializer.Serialize(result, options);
                    entity.UtcUpdated = now;
                    entity.State = EternityEntityState.Completed;
                    entity.UtcETA = now.Add(instance.PreserveTime);
                    entity.Priority = -1;
                    session?.LogInformation($"Workflow {entity.ID} completed.");
                }
                catch (ActivitySuspendedException)
                {
                    entity.State = EternityEntityState.Suspended;
                    await repository.SaveAsync(entity);
                    session?.LogVerbose($"Workflow {entity.ID} suspended.");
                    return;
                }
                catch (Exception ex)
                {
                    entity.Response = ex.ToString();
                    entity.State = EternityEntityState.Failed;
                    var now = clock.UtcNow;
                    entity.UtcUpdated = now;
                    entity.UtcETA = now.Add(instance?.FailurePreserveTime ?? TimeSpan.FromHours(24));
                    entity.Priority = -1;
                    session?.LogError($"Workflow {entity.ID} failed. {ex}");
                }
                if (entity.ParentID != null)
                {
                    await RaiseEventAsync(entity.ParentID, entity.ID!, "Success");
                    session?.LogVerbose($"Workflow {entity.ID} Raised Event for Parent {entity.ParentID}");
                }

                if (entity.ParentID != null)
                {
                    var parent = await repository.GetAsync(entity.ParentID);
                    if (parent != null)
                    {
                        parent.UtcETA = clock.UtcNow;
                        await repository.SaveAsync(entity, parent);
                        return;
                    }
                } 
                await repository.SaveAsync(entity);
            }
            catch (Exception ex)
            {
                session?.LogError(ex.ToString());
            }
        }

        internal async Task Delay(IWorkflow workflow, string id, DateTimeOffset timeout)
        {

            var key = CreateEntity(workflow, "Delay", false, Empty, timeout, workflow.CurrentUtc);
            var status = await repository.GetAsync(key.ID);

            switch (status?.State)
            {
                case EternityEntityState.Completed:
                    workflow.SetCurrentTime(status.UtcUpdated);
                    return;
                case EternityEntityState.Failed:
                    workflow.SetCurrentTime(status.UtcUpdated);
                    throw new ActivityFailedException(status.Response!);
            }

            var entity = workflow.Entity;

            var utcNow = clock.UtcNow;
            if (timeout <= utcNow)
            {
                // this was in the past...
                key.State = EternityEntityState.Completed;
                key.Response = "null";
                key.UtcUpdated = utcNow;
                entity.UtcUpdated = key.UtcUpdated;
                await repository.SaveAsync(key, entity);
                return;
            }

            var diff = timeout - utcNow;
            if (diff.TotalSeconds > 15)
            {
                await SaveWorkflow(entity, timeout);
                SetMaxPollingGap(diff);
                throw new ActivitySuspendedException();
            }

            await Task.Delay(diff, Cancellation);

            key.State = EternityEntityState.Completed;
            key.Response = "null";
            key.UtcUpdated = clock.UtcNow;
            workflow.SetCurrentTime(key.UtcUpdated);
            entity.UtcUpdated = key.UtcUpdated;
            await repository.SaveAsync(entity, key);
        }

        public async Task RaiseEventAsync(string id, string name, string result, bool throwIfNotFound = false)
        {
            var (workflow, existing) = await repository.GetEventAsync(id, nameof(WaitForExternalEventsAsync), Serialize(name));
            if (workflow == null)
            {
                if (throwIfNotFound)
                {
                    var error = $"Could not raise event {name}, Workflow {id} not found";
                    logger.LogError(error);
                    throw new ArgumentException(error);
                }
                logger?.Log(System.Diagnostics.TraceEventType.Warning, $"Could not raise event {name}, Workflow {id} not found");
                return;
            }
            if (existing == null) {
                if (throwIfNotFound)
                {
                    var error = $"Workflow with {id} is not waiting for any event";
                    logger.LogError(error);
                    throw new ArgumentException(error);
                }
                return;
            }
            if (existing.State == EternityEntityState.Failed || existing.State == EternityEntityState.Completed)
            {
                Trigger(existing.ID);
                return;
            }
            logger?.Log(System.Diagnostics.TraceEventType.Information, $"Raising event {name} for {id}");
            var now = clock.UtcNow;
            existing.UtcUpdated = now;
            existing.State = EternityEntityState.Completed;
            // existing.UtcETA = existing.UtcUpdated;
            existing.Response = Serialize(new EventResult { 
                EventName = name,
                Value = result
            });
            existing.Input = ""; // remove event names... 
            workflow.UtcETA = now;
            await repository.SaveAsync(workflow, existing);

            Trigger(existing.ID);
        }

        private EternityEntity CreateEntity(
            IWorkflow workflow,
            string name,
            bool uniqueParameters,
            object?[] parameters,
            DateTimeOffset eta,
            DateTimeOffset workflowUtcNow)
        {
            return EternityEntity.From(workflow.ID, name, uniqueParameters, parameters, eta, workflowUtcNow, options);
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

            var activityEntity = CreateEntity(workflow, methodName, uniqueParameters, input, after, workflow.CurrentUtc);

            while (true)
            {

                var entity = workflow.Entity;
                // has result...
                var task = await repository.GetAsync(activityEntity.ID);

                switch (task?.State)
                {
                    case EternityEntityState.Failed:
                        workflow.SetCurrentTime(task.UtcUpdated);
                        throw new ActivityFailedException(task.Response!);
                    case EternityEntityState.Completed:
                        workflow.SetCurrentTime(task.UtcUpdated);
                        if (typeof(TActivityOutput) == typeof(object))
                            return (TActivityOutput)(object)"null";
                        return JsonSerializer.Deserialize<TActivityOutput>(task.Response!, options)!;
                }

                await using var entityLock = await repository.LockAsync(entity, MaxLock);

                session?.LogVerbose($"Workflow {ID} Scheduling new activity {methodName}");                
                var diff = after - clock.UtcNow;
                if (diff.TotalMilliseconds > 0)
                {
                    await SaveWorkflow(entity, after);

                    if (diff.TotalSeconds > 15)
                    {
                        SetMaxPollingGap(diff);
                        throw new ActivitySuspendedException();
                    }

                    await Task.Delay(diff, this.Cancellation);
                }

                await RunActivityAsync(workflow, activityEntity, method, input);
            }


        }

        internal async Task RunActivityAsync(
            IWorkflow workflow,
            EternityEntity key, MethodInfo method, object?[] parameters)
        {
            using var session = this.logger.BeginLogSession();

            session?.LogVerbose($"Wrokflow {workflow.ID} executing activity {method.Name}");

            var type = this.EmitAvailable ? workflow.GetType().BaseType : workflow.GetType();


            try
            {

                using var scope = scopeFactory?.CreateScope(services);

                session?.LogVerbose($"Wrokflow {workflow.ID} running activity {method.Name}");
                workflow.IsActivityRunning = true;

                BuildParameters(method, parameters, scope?.ServiceProvider ?? services);

                // if type is generated...
                var result = (workflow.IsGenerated || !EmitAvailable)
                    ? await method.InvokeAsync(workflow, parameters, options)
                    : await method.RunAsync(workflow, parameters, options);

                key.Response = result;
                key.State = EternityEntityState.Completed;
                var now = clock.UtcNow;
                key.UtcUpdated = now;
                // key.UtcCreated = now;
                key.UtcETA = now;
                workflow.SetCurrentTime(now);
                workflow.Entity.UtcUpdated = now;
                await repository.SaveAsync(key, workflow.Entity);
                session?.LogVerbose($"Wrokflow {workflow.ID} executing activity finished");
                return;

            }
            catch (Exception ex) when (!(ex is ActivitySuspendedException))
            {
                var now = clock.UtcNow;
                key.Response = ex.ToString();
                key.State = EternityEntityState.Failed;
                key.UtcUpdated = now;
                // key.UtcCreated = now;
                key.UtcETA = now;
                workflow.SetCurrentTime(now);
                workflow.Entity.UtcUpdated = now;
                await repository.SaveAsync(key, workflow.Entity);
                session?.LogError($"Wrokflow {workflow.ID} executing activity failed {ex.ToString()}");
                throw new ActivityFailedException(ex.ToString());
            }
            finally
            {
                workflow.IsActivityRunning = false;
            }

            void BuildParameters(MethodInfo method, object?[] parameters, IServiceProvider? serviceProvider)
            {
                var pas = method.GetParameters();
                for (int i = 0; i < pas.Length; i++)
                {
                    var pa = pas[i];
                    if (pa.GetCustomAttribute<InjectAttribute>() == null)
                    {
                        continue;
                    }
                    if (serviceProvider == null)
                        throw new ArgumentNullException($"{nameof(serviceProvider)} is null");
                    var serviceRequested = serviceProvider.GetService(pa.ParameterType)
                        ?? throw new ArgumentException($"No service registered for {pa.ParameterType.FullName}");
                    parameters[i] = serviceRequested;
                }
            }
        }

        private T Deserialize<T>(string? response)
        {
            if (string.IsNullOrEmpty(response))
                return default!;
            return JsonSerializer.Deserialize<T>(response, this.options)!;
        }

        internal async Task<TOutput?> ChildAsync<TInput, TOutput>(
            IWorkflow workflow, Type childType, WorkflowOptions<TInput> input)
        {
            var utcNow = workflow.CurrentUtc;
            var eta = input.ETA ?? utcNow;
            var id = $"{workflow.ID}-{childType.AssemblyQualifiedName}";
            var key = new EternityEntity(id, childType.AssemblyQualifiedName, Serialize(input.Input));
            key.UtcETA = eta;
            key.UtcCreated = utcNow;
            key.UtcUpdated = key.UtcCreated;
            key.ParentID = workflow.ID;

            var result = await repository.GetAsync(key.ID);
            if (result == null)
            {
                await repository.SaveAsync(key);
            }
            else
            {
                if(result.State == EternityEntityState.Completed)
                {
                    return Deserialize<TOutput>(result.Response);
                }
                if(result.State == EternityEntityState.Failed)
                {
                    throw new ActivityFailedException(result.Response);
                }
            }

            throw new ActivitySuspendedException();
        }

        private static readonly object[] Empty = new object[] { };

        internal async Task<(string? name, string? value)> WaitForExternalEventsAsync(
            IWorkflow workflow,
            string[] names,
            DateTimeOffset eta)
        {
            var workflowEntity = workflow.Entity;

            using var session = this.logger.BeginLogSession();
            // this should fix the bug of sql server rounding off ticks...
            var time = workflow.CurrentUtc;
            var activity = CreateEntity(workflow, nameof(WaitForExternalEventsAsync), false, Empty, eta, time);
            activity.Input = Serialize(names);
            activity.Priority = workflow.WaitCount++;
            var activityId = activity.ID;
            workflowEntity.UtcETA = eta;
            session?.LogVerbose($"Workflow {activityId} waiting for an external event");
            var result = await repository.GetAsync(activityId);
            if (result == null)
            {
                session?.LogVerbose($"Workflow {activityId} created");
                await repository.SaveAsync(activity, workflowEntity);
            }

            while (true)
            {
                result = await repository.GetAsync(activityId);
                if (result == null)
                {
                    throw new InvalidOperationException($"Waiting Activity disposed");
                }

                switch (result.State)
                {
                    case EternityEntityState.Completed:
                        workflow.SetCurrentTime(result.UtcUpdated);
                        await SaveWorkflow(workflowEntity, eta);
                        var er = Deserialize<EventResult>(result.Response)!;
                        session?.LogVerbose($"Workflow {activityId} waiting for an external event finished {result.Response}");
                        return (er.EventName, er.Value);
                    case EternityEntityState.Failed:
                        workflow.SetCurrentTime(result.UtcUpdated);
                        await SaveWorkflow(workflowEntity, eta);
                        session?.LogVerbose($"Workflow {activityId} waiting for an external event failed {result.Response}");
                        throw new ActivityFailedException(result.Response!);
                }

                var diff = eta - clock.UtcNow;
                if (diff.TotalMilliseconds >0)
                {
                    await SaveWorkflow(workflowEntity, eta);
                
                    if (diff.TotalSeconds > 15)
                    {
                        SetMaxPollingGap(diff);
                        throw new ActivitySuspendedException();
                    }

                    await waitingTokens.Delay(activityId, diff, Cancellation);
                }

                result = await repository.GetAsync(activityId);
                if (result == null || (result.State != EternityEntityState.Completed && result.State != EternityEntityState.Failed))
                {

                    var timedout = new EventResult { };
                    activity.Response = Serialize(timedout);
                    activity.State = EternityEntityState.Completed;
                    activity.Input = "";
                    var now = clock.UtcNow;
                    activity.UtcUpdated = now;
                    workflow.SetCurrentTime(now);
                    workflowEntity.UtcUpdated = now;
                    workflowEntity.UtcETA = now;
                    session?.LogVerbose($"Workflow {activityId} timed out.");
                    await repository.SaveAsync(activity, workflowEntity);
                    return (null, null);
                }
            }
        }

        private async Task SaveWorkflow(EternityEntity workflowEntity, DateTimeOffset eta)
        {
            workflowEntity.UtcETA = eta;
            workflowEntity.UtcUpdated = clock.UtcNow;
            await repository.SaveAsync(workflowEntity);
        }

        internal string Serialize<TInput>(TInput input)
        {
            return JsonSerializer.Serialize(input, options);
        }
    }
}
