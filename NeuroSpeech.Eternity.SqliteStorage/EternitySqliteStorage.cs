using Microsoft.Data.Sqlite;
using NeuroSpeech.TemplatedQuery;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace NeuroSpeech.Eternity
{
    public class EternitySqliteStorage : IEternityStorage
    {

        private Task? InitAsync;
        private readonly TimeSpan QueuePollInterval;
        private readonly string connectionString;
        private readonly IEternityClock clock;

        public EternitySqliteStorage(
            string connectionString, 
            IEternityClock clock,
            TimeSpan queuePollInterval = default)
        {
            this.QueuePollInterval = queuePollInterval.Ticks == 0
                ? TimeSpan.FromSeconds(15)
                : queuePollInterval;
            this.connectionString = connectionString;
            this.clock = clock;
        }

        public async Task<SqliteConnection> Open()
        {
            var conn = new SqliteConnection(connectionString);
            if(InitAsync == null)
            {
                InitAsync = ModelCreator.CreateAsync(conn);
            }
            await InitAsync;
            return conn;
        }

        public async Task<List<WorkflowStep>> EnumerateWorkflowsAsync(int start, int size = 100)
        {
            using var db = await Open();
            var q = TemplateQuery.New($"SELECT * FROM Workflows ORDER BY DateCreated DESC LIMIT {size} OFFSET {start}");
            return await db.FromSqlAsync<WorkflowStep>(q, true);
        }

        public async Task<IEternityLock> AcquireLockAsync(string id, long sequenceId)
        {
            while (true)
            {
                using var db = await Open(); 
                try {
                    var q = TemplateQuery.New(@$"INSERT INTO ActivityLocks(SequenceID) VALUES ({sequenceId});");
                    await db.ExecuteNonQueryAsync(q);
                    return new ActivityLock { SequenceID = sequenceId };
                }
                catch (SqliteException ex){
                    System.Diagnostics.Debug.WriteLine(ex);
                }
                await Task.Delay(QueuePollInterval);
            }
        }

        public async Task DeleteHistoryAsync(string id)
        {
            using var db = await Open();
            var query = TemplateQuery.New(@$"
DELETE FROM Activities WHERE ID={id};
DELETE FROM ActivityEvents WHERE ID={id};
");
            await db.ExecuteNonQueryAsync(query);
        }

        public async Task FreeLockAsync(IEternityLock executionLock)
        {
            using var db = await Open();
            var l = (executionLock as ActivityLock)!;
            var q = TemplateQuery.New(@$"DELETE FROM ActivityLocks WHERE SequenceID={l.SequenceID}");
            await db.ExecuteNonQueryAsync(q);
        }

        public async Task<ActivityStep> GetEventAsync(string id, string eventName)
        {
            using var db = await Open(); var sids = await db.FromSqlAsync<ActivityStep>(TemplateQuery.New(@$"
SELECT SequenceID FROM ActivityEvents WHERE ID={id} AND EventName={eventName}
"), true);
            return await GetStatusAsync(sids.First());
        }

        private static long lockID = 1;

        public async Task<WorkflowQueueItem[]> GetScheduledActivitiesAsync()
        {
            using var db = await Open();
            var now = clock.UtcNow;
            var locked = now.AddMinutes(15);
            var q = TemplateQuery.New($"SELECT * FROM QueueTokens WHERE ETA <= {now.UtcTicks} LIMIT 32");
            var list = await db.FromSqlAsync<QueueToken>(q, true);
            List<WorkflowQueueItem> results = new List<WorkflowQueueItem>();
            foreach (var c in list)
            {
                if (c.ETALocked > now.UtcTicks)
                    continue;
                var lid = Interlocked.Increment(ref lockID);
                // update single...
                int i = await db.ExecuteNonQueryAsync(TemplateQuery.New(@$"
UPDATE 
    QueueTokens
SET
    ETALocked={locked.Ticks},
    CID={lid}
WHERE
    Token={c.Token}
    AND CID={c.CID}
"));

                if (i == 1)
                {
                    results.Add(new WorkflowQueueItem
                    {
                        ID = c.ID,
                        QueueToken = c.Token.ToString()
                    });
                }
            }
            return results.ToArray();
        }

        public async Task<ActivityStep> GetStatusAsync(ActivityStep key)
        {
            using var db = await Open();
            if (key.SequenceID > 0)
            {
                return await db.FirstOrDefaultAsync<ActivityStep>(TemplateQuery.New(@$"
SELECT * FROM Activities WHERE SequenceID = {key.SequenceID}
"), true);
            }

            var q = TemplateQuery.New(@$"SELECT * FROM Activities WHERE ID={key.ID} AND KeyHash={key.KeyHash} AND Key={key.Key}");
            return await db.FirstOrDefaultAsync<ActivityStep>(q, true);
        }

        public async Task<WorkflowStep> GetWorkflowAsync(string id)
        {
            using var db = await Open();
            var query = TemplateQuery.New($"SELECT * FROM Workflows WHERE ID = {id}");
            var list = await db.FromSqlAsync<WorkflowStep>(query, true);
            return list.FirstOrDefault();
        }

        public async Task<ActivityStep> InsertActivityAsync(ActivityStep key)
        {
            using var db = await Open();
            var query = TemplateQuery.New($@"
INSERT INTO Activities (
ID,
Method,
ActivityType,
DateCreated,
LastUpdated,
ETA,
Key,
KeyHash,
Status,
Error,
Result
)
VALUES (
    {key.ID},
    {key.Method},
    {key.ActivityType},
    {key.DateCreated.UtcTicks},
    {key.LastUpdated.UtcTicks},
    {key.ETA.UtcTicks},
    {key.Key},
    {key.KeyHash},
    {key.Status.ToString()},
    {key.Error},
    {key.Result}
);
SELECT last_insert_rowid();");
            var id = await db.ExecuteScalarAsync(query);
            key.SequenceID = (long)Convert.ChangeType(id, typeof(long));

            // insert if it has any events..
            if (key.ActivityType == ActivityType.Event)
            {
                var eventNames = key.GetEvents()!;
                foreach (var name in eventNames)
                {
                    var insert = TemplateQuery.New($"INSERT INTO ActivityEvents(ID,EventName,SequenceID) VALUES ({key.ID}, {name}, {key.SequenceID})");
                    await db.ExecuteNonQueryAsync(insert);
                }
            }

            return key;
        }

        public async Task<WorkflowStep> InsertWorkflowAsync(WorkflowStep step)
        {
            using var db = await Open();
            var query = TemplateQuery.New(@$"
    INSERT INTO Workflows (
        ID,
        WorkflowType,
        Category,
        Parameter,
        ETA,
        DateCreated,
        LastUpdated,
        Status,
        Result,
        Error
    )
    VALUES (
        {step.ID},
        {step.WorkflowType},
        {step.Category},
        {step.Parameter},
        {step.ETA.UtcTicks},
        {step.DateCreated.UtcTicks},
        {step.LastUpdated.UtcTicks},
        {step.Status.ToString()},
        {step.Result},
        {step.Error}
    )
");
            await db.ExecuteNonQueryAsync(query);
            return step;
        }

        public async Task<string> QueueWorkflowAsync(WorkflowQueueItem item, string? existing = null)
        {
            using var db = await Open();
            if (existing != null)
            {
                var eid = long.Parse(existing);
                await db.ExecuteNonQueryAsync(TemplateQuery.New($@"DELETE FROM QueueTokens WHERE Token={eid}"));
            }
            var q = TemplateQuery.New(@$"
INSERT INTO QueueTokens(ID,ETA,Command,ETALocked,CID)
VALUES (
    {item.ID},
    {item.ETA.UtcTicks},
    {item.Command},
    {0},
    {0}
);
SELECT last_insert_rowid();");
            var nextID = await db.ExecuteScalarAsync(q);
            return nextID.ToString();
        }

        public async Task RemoveQueueAsync(params string[] token)
        {
            using var db = await Open();
            long[] ids = new long[token.Length];
            for (int i = 0; i < token.Length; i++)
            {
                ids[i] = long.Parse(token[i]);
            }
            await db.ExecuteNonQueryAsync(TemplateQuery.New(@$"DELETE FROM QueueTokens WHERE Token IN ({ids})"));
        }

        public async Task UpdateAsync(ActivityStep key)
        {
            using var db = await Open();
            var q = TemplateQuery.New(@$"
UPDATE 
    Activities 
SET
    DateCreated={key.DateCreated.UtcTicks},
    LastUpdated={key.LastUpdated.UtcTicks},
    ETA={key.ETA.UtcTicks},
    Status={key.Status.ToString()},
    Error={key.Error},
    Result={key.Result},
    QueueToken={key.QueueToken}
WHERE
    SequenceID={key.SequenceID}
");

            await db.ExecuteNonQueryAsync(q);
        }

        public async Task UpdateAsync(WorkflowStep key)
        {
            using var db = await Open();
            var q = TemplateQuery.New(@$"
UPDATE 
    Workflows 
SET
    LastUpdated={key.LastUpdated.UtcTicks},
    ETA={key.ETA.UtcTicks},
    Status={key.Status.ToString()},
    Error={key.Error},
    Result={key.Result}
WHERE
    ID={key.ID}
");

            await db.ExecuteNonQueryAsync(q);
        }

        public async Task DeleteWorkflowAsync(string id)
        {
            using var db = await Open();
            var query = TemplateQuery.New(@$"
DELETE FROM Activities WHERE ID={id};
DELETE FROM ActivityEvents WHERE ID={id};
DELETE FROM Workflows WHERE ID={id};
");
            db.ExecuteNonQuery(query);
        }
    }
}
