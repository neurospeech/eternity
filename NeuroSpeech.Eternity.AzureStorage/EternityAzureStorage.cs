using Azure;
using Azure.Data.Tables;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Specialized;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace NeuroSpeech.Eternity
{
    public class EternityAzureStorage : IEternityStorage
    {
        private readonly TableServiceClient TableClient;
        // private readonly QueueClient QueueClient;
        private readonly TableClient Activities;
        private readonly TableClient Workflows;
        private readonly TableClient ActivityQueue;
        private readonly BlobContainerClient Locks;
        private readonly BlobContainerClient ParamStorage;
        

        public EternityAzureStorage(string prefix, string connectionString, bool createStorage = false)
        {
            this.TableClient = new TableServiceClient(connectionString);
            // this.QueueClient = new QueueServiceClient(connectionString).GetQueueClient($"{prefix}Workflows".ToLower());
            var storageClient = new BlobServiceClient(connectionString);
            this.Activities = TableClient.GetTableClient($"{prefix}Activities".ToLower());
            this.Workflows = TableClient.GetTableClient($"{prefix}Workflows".ToLower());
            this.ActivityQueue = TableClient.GetTableClient($"{prefix}Queue".ToLower());
            this.Locks = storageClient.GetBlobContainerClient($"{prefix}Locks".ToLower());
            this.ParamStorage = storageClient.GetBlobContainerClient($"{prefix}ParamStorage".ToLower());


            // QueueClient.CreateIfNotExists();
            if (createStorage)
            {
                try
                {
                    Activities.CreateIfNotExists();
                }
                catch { }
                try
                {
                    Workflows.CreateIfNotExists();
                }
                catch { }
                try { ActivityQueue.CreateIfNotExists(); } catch { }
                try
                {
                    Locks.CreateIfNotExists();
                }
                catch { }
                try { ParamStorage.CreateIfNotExists(); } catch { }
            }
        }

        public async Task<IEternityLock> AcquireLockAsync(string id, long sequenceId)
        {
            for (int i = 0; i < 30; i++)
            {
                try
                {

                    var lockName = $"{id}-{sequenceId}.lock";
                    var b = Locks.GetBlobClient(lockName);
                    if(!(await b.ExistsAsync()))
                    {
                        await b.UploadAsync(new MemoryStream(new byte[] { 1, 2, 3 }));
                    }
                    var bc = b.GetBlobLeaseClient();
                    var r = await bc.AcquireAsync(TimeSpan.FromSeconds(59));
                    return new EternityBlobLock
                    {
                        LeaseID = r.Value.LeaseId,
                        LockName = lockName
                    };
                } catch (Exception)
                {
                    await Task.Delay(20000);
                }
            }
            throw new InvalidOperationException();
        }

        public async Task FreeLockAsync(IEternityLock executionLock)
        {
            try
            {
                var el = executionLock as EternityBlobLock;
                var b = Locks.GetBlobClient(el.LockName);
                var bc = b.GetBlobLeaseClient(el.LeaseID);
                await bc.ReleaseAsync();
                await b.DeleteIfExistsAsync();
            }catch (Exception) { }
        }

        public async Task<ActivityStep> GetEventAsync(string id, string eventName)
        {
            var name = $"E-{eventName}";
            var filter = Azure.Data.Tables.TableClient.CreateQueryFilter($"PartitionKey eq {id} and RowKey eq {name}");
            string keyHash = null;
            await foreach (var e in Activities.QueryAsync<TableEntity>(filter, 1))
            {
                keyHash = e.GetString("StepRowKey");
            }
            // this is the case when History may be deleted
            if (keyHash == null)
                return null;
            filter = Azure.Data.Tables.TableClient.CreateQueryFilter($"PartitionKey eq {id} and RowKey eq {keyHash}");
            await foreach (var e in Activities.QueryAsync<TableEntity>(filter))
            {
                if (e.ContainsKey("Key"))
                    return e.ToObject<ActivityStep>();
                var url = e["KeyUrl"].ToString();
                var blob = ParamStorage.GetBlobClient(url);
                var stepKey = await blob.DownloadTextAsync();
                var a = e.ToObject<ActivityStep>();
                a.Key = stepKey;
                return a;
            }
            return null;
        }

        public async Task<WorkflowQueueItem[]> GetScheduledActivitiesAsync(int maxActivitiesToProcess)
        {
            // It is not good to submit multiple queue locking transaction in bulk
            // we want to get at least one message to process queue, chances of failing single message
            // is far less than chances of failing all messages at once.

            var now = DateTimeOffset.UtcNow;
            var nowTicks = now.UtcTicks;

            var locked = now.AddSeconds(60).UtcTicks;

            var list = new List<TableEntity>();
            await foreach(var item in ActivityQueue.QueryAsync<TableEntity>())
            {
                var eta = item.GetDateTimeOffset("ETA").Value.UtcTicks;
                if(eta > nowTicks)
                {
                    break;
                }
                var entityLocked = item.GetInt64("Locked").GetValueOrDefault();
                if (entityLocked != 0 && entityLocked < locked)
                {
                    continue;
                }
                item["Locked"] = locked;
                try
                {
                    await ActivityQueue.UpdateEntityAsync(item, item.ETag);
                    list.Add(item);
                    if (list.Count == maxActivitiesToProcess)
                        break;
                }
                catch (RequestFailedException re)
                {
                    if (re.Status == 419)
                        continue;
                    throw;
                }
            }
            return list.Select(x => new WorkflowQueueItem {
                ID = x.GetString("Message"),
                QueueToken = $"{x.PartitionKey},{x.RowKey},{x.ETag}",
                Command = x.TryGetValue("Command", out var c) ? c.ToString() : null
            }).ToArray();
        }

        public async Task<ActivityStep> GetStatusAsync(ActivityStep key)
        {

            // Find SequenceID first..
            var prefix = $"H-{key.KeyHash}-";
            var next = $"H-{key.KeyHash}-9999999999999999";
            var filter = Azure.Data.Tables.TableClient.CreateQueryFilter($"PartitionKey eq {key.ID} and RowKey ge {prefix} and RowKey lt {next}");
            await foreach (var e in Activities.QueryAsync<TableEntity>(filter)) {
                if (e.TryGetValue("Key", out var k))
                {
                    if (k.ToString() == key.Key) {
                        return e.ToObject<ActivityStep>();
                    }
                    continue;
                }
                var url = e["KeyUrl"].ToString();
                var blob = ParamStorage.GetBlobClient(url);
                var stepKey = await blob.DownloadTextAsync();
                if(stepKey == key.Key)
                {
                    var a = e.ToObject<ActivityStep>();
                    a.Key = stepKey;
                    return a;
                }
            }
            return null;
        }

        public async Task<WorkflowStep> GetWorkflowAsync(string id)
        {
            await foreach(var e in Workflows.QueryAsync<TableEntity>(x => x.PartitionKey == id && x.RowKey == "1"))
            {
                return e.ToObject<WorkflowStep>();
            }
            return null;
        }

        public async Task<ActivityStep> InsertActivityAsync(ActivityStep key)
        {
            // generate new id...
            long id = await Activities.NewSequenceIDAsync(key.ID, "ID");
            key.SequenceID = id;
            var rowKey = $"H-{key.KeyHash}-{id}";
            var entity = key.ToTableEntity(rowKey);
            if(key.Key.Length > 30000)
            {
                // store it in blob..
                var keyUrl = key.ID + "/" + rowKey;
                var blob = ParamStorage.GetBlockBlobClient(keyUrl);
                var ms = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(key.Key));
                await blob.UploadAsync(ms);
                entity.Add("KeyUrl", keyUrl);
                entity.Remove(nameof(key.Key));
            } else
            {
                entity.Add("Key", key.Key);
            }
            var actions = new List<TableTransactionAction>
            {
                new TableTransactionAction(TableTransactionActionType.UpsertReplace, entity, ETag.All)
            };
            // last active event waiting must be added with eventName
            if (key.ActivityType == ActivityType.Event)
            {
                string[] eventNames = key.GetEvents();
                foreach(var name in eventNames)
                {
                    actions.Add(new TableTransactionAction(TableTransactionActionType.UpsertReplace, new TableEntity(key.ID, "E-" + name)
                    {
                        { "StepRowKey", rowKey }
                    }, ETag.All));
                }
            }
            await Activities.SubmitTransactionAsync(actions);
            return key;
        }

        public async Task<WorkflowStep> InsertWorkflowAsync(WorkflowStep step)
        {
            await UpdateAsync(step);
            return step;
        }

        public async Task<string> QueueWorkflowAsync(WorkflowQueueItem item, string existing = null)
        {
            if (existing != null)
            {
                await RemoveQueueAsync(existing);
            }
            var utc = item.ETA;
            var day = utc.Date.Ticks.ToStringWithZeros();
            var time = utc.TimeOfDay.Ticks.ToStringWithZeros();
            for (long sid = DateTime.UtcNow.Ticks; sid <= long.MaxValue; sid++)
            {
                try
                {
                    var key = $"{time}-{sid.ToStringWithZeros()}";
                    var eq = new TableEntity(day, key) {
                        { "Message", item.ID },
                        { "ETA", item.ETA }
                    };
                    if(item.Command != null)
                    {
                        eq.Add("Command", item.Command);
                    }
                    var r = await ActivityQueue.AddEntityAsync(eq);
                    return $"{day},{key},{r.Headers.ETag.GetValueOrDefault()}";
                }
                catch (RequestFailedException ex)
                {
                    if (ex.Status == 409)
                        continue;
                }
            }
            throw new UnauthorizedAccessException();
        }

        public Task RemoveQueueAsync(params string[] tokens)
        {
            return ActivityQueue.DeleteAllAsync(tokens.Select(x => {
                var tokens = x.Split(',');
                return (tokens[0], tokens[1]);
            }));
        }

        public Task UpdateAsync(ActivityStep key)
        {
            return Activities.UpsertEntityAsync(key.ToTableEntity($"H-{key.KeyHash}-{key.SequenceID}"));
        }

        public Task UpdateAsync(WorkflowStep key)
        {
            return Workflows.UpsertEntityAsync(key.ToTableEntity(key.ID, "1"), TableUpdateMode.Replace);
        }

        public async Task DeleteHistoryAsync(string id)
        {
            await foreach(var b in ParamStorage.GetBlobsAsync(Azure.Storage.Blobs.Models.BlobTraits.All, Azure.Storage.Blobs.Models.BlobStates.All, id + "/"))
            {
                await ParamStorage.DeleteBlobIfExistsAsync(b.Name);
            }
            await Activities.DeleteAllAsync(id);
        }

        public async Task DeleteWorkflowAsync(string id)
        {
            await DeleteHistoryAsync(id);
            await Workflows.DeleteAllAsync(id);
        }
    }
}
