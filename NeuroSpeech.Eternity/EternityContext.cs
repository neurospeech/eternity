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

        private Task RaiseEventAsync(string parentID, string v1, string v2)
        {
            throw new NotImplementedException();
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

            var dt = uniqueParameters ? "" : $"-{clock.UtcNow.Ticks}";

            var inputJson = new string[input.Length];
            var uk = new StringBuilder();
            for (int i = 0; i < input.Length; i++)
            {
                var json = JsonSerializer.Serialize(input[i], options);
                inputJson[i] = json;
                uk.Append(json);
                uk.Append(',');
            }

            var activityID = $"{workflow.ID}{dt}-{method.Name}-{uk}";


            while (true)
            {

                var entity = workflow.Entity;
                await using var entityLock = await repository.LockAsync(entity, MaxLock);

                // has result...
                var task = await repository.GetAsync(activityID);

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
                    entity.UtcETA = after.UtcDateTime;
                    await repository.SaveAsync(entity);

                    if (diff.TotalSeconds > 15)
                    {
                        throw new ActivitySuspendedException();
                    }

                    await Task.Delay(diff, this.Cancellation);
                }

                await RunActivityAsync(workflow, activityID, method, input, inputJson);
            }


        }

        internal async Task RunActivityAsync(IWorkflow workflow, string acivityID, MethodInfo method, object?[] parameters, string[] inputJson)
        {
            using var session = this.logger.BeginLogSession();

            session?.LogInformation($"Wrokflow {workflow.ID} executing activity {method.Name}");

            var type = this.EmitAvailable ? workflow.GetType().BaseType : workflow.GetType();

            var key = new EternityEntity(acivityID, method.Name, inputJson);


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


        internal async Task<TOutput?> ChildAsync<TInput, TOutput>(
            IWorkflow workflow, Type childType, WorkflowOptions<TInput> input)
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
            if (r.name == status.Result)
            {
                var ws = await storage.GetWorkflowAsync(r.name!);
                if (ws.Status == ActivityStatus.Failed)
                {
                    throw new ActivityFailedException(ws.Error!);
                }
                return ws.AsResult<TOutput?>(options);
            }
            throw new TimeoutException();
        }
    }
}
