using NeuroSpeech.Eternity.Converters;
using System;
using System.Collections.Concurrent;
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
        private readonly IServiceProvider services;
        private readonly IEternityClock clock;
        private readonly IEternityRepository repository;
        private readonly IEternityLogger? logger;
        private readonly IServiceScopeFactory? scopeFactory;

        public event EventHandler? NewWorkflow;

        private WaitingTokens waitingTokens;
        private readonly JsonSerializerOptions options;

        /// <summary>
        /// Please turn off EmitAvailable on iOS
        /// </summary>
        public bool EmitAvailable { get; set; } = true;

        public CancellationToken Cancellation { get; private set; }

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
            NewWorkflow?.Invoke(this, EventArgs.Empty);
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

        internal Task<string> CreateAsync<TInput>(Type type, WorkflowOptions<TInput> options)
        {
            var id = options.ID ?? Guid.NewGuid().ToString("N");
            return repository.CreateAsync(new EternityEntity(
                id,
                type.AssemblyQualifiedName,
                JsonSerializer.Serialize(options.Input, this.options)));
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
                LastUpdate = result.UtcUpdated
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
            var items = await repository.QueryAsync(maxActivitiesToProcess);
            if (items.Count == 0)
                return items.Count;
            using var ws = new WorkflowScheduler<EternityEntity>(cancellationToken);
            var tasks = new Task[items.Count];
            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                tasks[i] = ws.Queue(item.ID, item, RunWorkflowAsync);
            }
            await Task.WhenAll(tasks);
            waitingTokens.Clear();
            return items.Count;
        }

        private static TimeSpan MaxLock = TimeSpan.FromMinutes(1);

        private IWorkflow GetWorkflowInstance(EternityEntity entity, Type type, string id, DateTimeOffset eta)
        {
            var w = (Activator.CreateInstance(type) as IWorkflow)!;
            w.Init(id, this, eta, type.GetCustomAttribute<GeneratedWorkflowAttribute>() != null);
            w.Entity = entity;
            return w;
        }


        private async Task RunWorkflowAsync(EternityEntity entity, CancellationToken arg2)
        {
            using var session = this.logger.BeginLogSession();
            try
            {
                var originalType = Type.GetType(entity.Name);
                var workflowType = this.GetDerived(originalType);
                // we need to begin...
                var instance = GetWorkflowInstance(entity, workflowType, entity.ID, entity.UtcUpdated);

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

                try
                {
                    var input = JsonSerializer.Deserialize(entity.Parameters[0], instance.InputType, options);
                    var result = await instance.RunAsync(input!);
                    entity.Response = JsonSerializer.Serialize(result, options);
                    entity.UtcUpdated = clock.UtcNow.UtcDateTime;
                    entity.State = EternityEntityState.Completed;
                    entity.UtcETA = clock.UtcNow.Add(instance.PreserveTime).UtcDateTime;
                    session?.LogInformation($"Workflow {entity.ID} completed.");
                }
                catch (ActivitySuspendedException)
                {
                    entity.State = EternityEntityState.Suspended;
                    await repository.SaveAsync(entity);
                    session?.LogInformation($"Workflow {entity.ID} suspended.");
                    return;
                }
                catch (Exception ex)
                {
                    entity.Response = ex.ToString();
                    entity.State = EternityEntityState.Failed;
                    entity.UtcUpdated = clock.UtcNow.UtcDateTime;
                    entity.UtcETA = clock.UtcNow.Add(instance.PreserveTime).UtcDateTime;
                    session?.LogInformation($"Workflow {entity.ID} failed. {ex.ToString()}");
                }
                if (entity.ParentID != null)
                {
                    await RaiseEventAsync(entity.ParentID, entity.ID!, "Success");
                    session?.LogInformation($"Workflow {entity.ID} Raised Event for Parent {entity.ParentID}");
                }

            }
            catch (Exception ex)
            {
                session?.LogError(ex.ToString());
            }
        }

        internal async Task Delay(IWorkflow workflow, string id, DateTimeOffset timeout)
        {

            var key = CreateEntity(workflow.ID, "Delay", true, Empty, timeout);
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
                key.UtcUpdated = utcNow.UtcDateTime;
                entity.UtcUpdated = key.UtcUpdated;
                await repository.SaveAsync(key, entity);
                return;
            }

            var diff = timeout - utcNow;
            if (diff.TotalSeconds > 15)
            {
                await SaveWorkflow(entity, timeout);
                throw new ActivitySuspendedException();
            }

            await Task.Delay(diff, Cancellation);

            key.State = EternityEntityState.Completed;
            key.Response = "null";
            key.UtcUpdated = clock.UtcNow.UtcDateTime;
            workflow.SetCurrentTime(key.UtcUpdated);
            entity.UtcUpdated = key.UtcUpdated;
            await repository.SaveAsync(entity, key);
        }

        public async Task RaiseEventAsync(string id, string name, string result, bool throwIfNotFound = false)
        {
            var entity = CreateEntity(id, nameof(WaitForExternalEventsAsync), true, Empty, clock.UtcNow);
            var existing = await repository.GetAsync(entity.ID);
            if (existing == null) {
                if (throwIfNotFound)
                {

                }
            }
            if (existing.State == EternityEntityState.Failed || existing.State == EternityEntityState.Completed)
            {
                // something wrong...
            }
            existing ??= entity;
            existing.UtcUpdated = clock.UtcNow.UtcDateTime;
            existing.UtcETA = existing.UtcUpdated;
            existing.Response = Serialize(new EventResult { 
                EventName = name,
                Value = result
            });

            var workflowEntity = await repository.GetAsync(id);
            workflowEntity.UtcETA = existing.UtcETA;
            await repository.SaveAsync(workflowEntity, existing);
        }
        
        private EternityEntity CreateEntity(string ID, string name, bool uniqueParameters, object?[] parameters, DateTimeOffset eta)
        {
            return EternityEntity.From(ID, name, uniqueParameters, parameters, eta, clock.UtcNow, options);
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

            var activityEntity = CreateEntity(ID, methodName, uniqueParameters, input, after);

            while (true)
            {

                var entity = workflow.Entity;
                await using var entityLock = await repository.LockAsync(entity, MaxLock);

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

                session?.LogInformation($"Workflow {ID} Scheduling new activity {methodName}");                
                var diff = after - clock.UtcNow;
                if (diff.TotalMilliseconds > 0)
                {
                    await SaveWorkflow(entity, after);

                    if (diff.TotalSeconds > 15)
                    {
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

            session?.LogInformation($"Wrokflow {workflow.ID} executing activity {method.Name}");

            var type = this.EmitAvailable ? workflow.GetType().BaseType : workflow.GetType();


            try
            {

                using var scope = scopeFactory?.CreateScope(services);

                session?.LogInformation($"Wrokflow {workflow.ID} running activity {method.Name}");
                workflow.IsActivityRunning = true;

                // if type is generated...
                var result = (workflow.IsGenerated || !EmitAvailable)
                    ? await method.InvokeAsync(workflow, parameters, options)
                    : await method.RunAsync(workflow, parameters, options);

                key.Response = JsonSerializer.Serialize(result, this.options);
                key.State = EternityEntityState.Completed;
                var now = clock.UtcNow.UtcDateTime;
                key.UtcUpdated = now;
                key.UtcCreated = now;
                key.UtcETA = now;
                workflow.SetCurrentTime(now);
                await repository.SaveAsync(key);
                session?.LogInformation($"Wrokflow {workflow.ID} executing activity finished");
                return;

            }
            catch (Exception ex) when (!(ex is ActivitySuspendedException))
            {
                var now = clock.UtcNow.UtcDateTime;
                key.Response = ex.ToString();
                key.State = EternityEntityState.Failed;
                key.UtcUpdated = now;
                key.UtcCreated = now;
                key.UtcETA = now;
                workflow.SetCurrentTime(now);
                await repository.SaveAsync(key);
                session?.LogError($"Wrokflow {workflow.ID} executing activity failed {ex.ToString()}");
                throw new ActivityFailedException(ex.ToString());
            }
            finally
            {
                workflow.IsActivityRunning = false;
            }
        }

        private string Serialize<T>(T model)
        {
            return JsonSerializer.Serialize<T>(model, this.options);
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
            var utcNow = clock.UtcNow;
            input.ETA ??= utcNow;
            var id = input.ID ?? Guid.NewGuid().ToString("N");
            var key = CreateEntity(id, childType.AssemblyQualifiedName, true, new object?[] { input.Input }, utcNow);
            key.ParentID = workflow.ID;

            // now wait for event..
            var r = await this.WaitForExternalEventsAsync(workflow, new string[] { key.Name }, utcNow.AddDays(1));
            if (r.name == "Child")
            {
                var ws = await repository.GetAsync(key.ID);
                if (ws == null || ws.State == EternityEntityState.Failed)
                {
                    throw new ActivityFailedException(ws?.Response ?? "Unknown Error");
                }
                return Deserialize<TOutput?>(ws.Response);
            }
            throw new TimeoutException();
        }

        private static readonly object[] Empty = new object[] { };

        internal async Task<(string? name, string? value)> WaitForExternalEventsAsync(
            IWorkflow workflow,
            string[] names,
            DateTimeOffset eta)
        {
            var workflowEntity = workflow.Entity;

            using var session = this.logger.BeginLogSession();
            session?.LogInformation($"Workflow {workflow.ID} waiting for an external event");
            var activity = CreateEntity(workflow.ID, nameof(WaitForExternalEventsAsync), true, Empty, eta);

            var activityId = activity.ID;
            workflowEntity.UtcETA = eta.UtcDateTime;
            var result = await repository.GetAsync(activityId);
            if (result != null && result.UtcETA != eta)
            {
                // lets save copy..
                var copy = CreateEntity(workflow.ID, nameof(WaitForExternalEventsAsync) + result.UtcETA, true, Empty, result.UtcETA);
                result.ID = copy.ID;
                await repository.SaveAsync(result, activity, workflowEntity);
            } else
            {
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
                        session?.LogInformation($"Workflow {workflow.ID} waiting for an external event finished {result.Response}");
                        return (er.EventName, er.Value);
                    case EternityEntityState.Failed:
                        workflow.SetCurrentTime(result.UtcUpdated);
                        await SaveWorkflow(workflowEntity, eta);
                        session?.LogInformation($"Workflow {workflow.ID} waiting for an external event failed {result.Response}");
                        throw new ActivityFailedException(result.Response!);
                }

                var diff = eta - clock.UtcNow;
                if (diff.TotalSeconds > 15)
                {
                    await SaveWorkflow(workflowEntity, eta);
                    throw new ActivitySuspendedException();
                }

                if (diff.TotalMilliseconds > 0)
                {
                    await Task.Delay(diff, this.Cancellation);
                }

                result = await repository.GetAsync(activityId);
                if (result == null || (result.State != EternityEntityState.Completed && result.State != EternityEntityState.Failed))
                {

                    var timedout = new EventResult { };
                    activity.Response = Serialize(timedout);
                    activity.State = EternityEntityState.Completed;
                    activity.UtcUpdated = clock.UtcNow.UtcDateTime;
                    workflow.SetCurrentTime(activity.UtcUpdated);
                    workflowEntity.UtcUpdated = activity.UtcUpdated;
                    workflowEntity.UtcETA = activity.UtcETA;
                    await repository.SaveAsync(activity, workflowEntity);
                    return (null, null);
                }
            }
        }

        private async Task SaveWorkflow(EternityEntity workflowEntity, DateTimeOffset eta)
        {
            workflowEntity.UtcETA = eta.UtcDateTime;
            workflowEntity.UtcUpdated = clock.UtcNow.UtcDateTime;
            await repository.SaveAsync(workflowEntity);
        }
    }
}
