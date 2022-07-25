using MySql.Data.MySqlClient;
using NeuroSpeech.Eternity.Storage;
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

    public class MySqlEternityEntity : EternityEntity
    {
        public long NID { get; set; }
    }
    public class EternityMySqlStorage : IEternityRepository
    {

        private Task? InitAsync;
        private readonly TimeSpan QueuePollInterval;
        private readonly string connectionString;
        private readonly IEternityClock clock;
        private readonly string tableName;

        public EternityMySqlStorage(
            string connectionString, 
            IEternityClock clock,
            string tableName = "workflows",
            TimeSpan queuePollInterval = default)
        {
            this.QueuePollInterval = queuePollInterval.Ticks == 0
                ? TimeSpan.FromSeconds(15)
                : queuePollInterval;
            this.connectionString = connectionString;
            this.clock = clock;
            this.tableName = tableName;
        }

        public async Task<MySqlConnection> Open()
        {
            var conn = new MySqlConnection(connectionString);
            if(InitAsync == null)
            {
                InitAsync = ModelCreator.CreateAsync(conn, tableName);
            }
            await InitAsync;
            return conn;
        }

        public async Task<List<EternityEntity>> DequeueAsync(int max, DateTimeOffset utcNow)
        {
            using var db = await Open();
            var now = utcNow.UtcDateTime;
            var futureLock = now.AddMinutes(1);
            var query = TemplateQuery.New(@$"
                SELECT *
                FROM {tableName}
                WHERE 
                    UtcETA < {now}
                    AND IsWorkflow=1
                    AND (QueueTTL IS NULL OR QueueTTL<{now})
                ORDER BY Priority DESC
                LIMIT {max}");
            var list = await db.FromSqlAsync<MySqlEternityEntity>(query, ignoreUnmatchedProperties: true);
            var rlist = new List<EternityEntity>();
            var ids = new List<long>();
            foreach (var item in list)
            {
                rlist.Add(item);
                ids.Add(item.NID);
            }
            if (ids.Any())
            {
                await db.ExecuteNonQueryAsync(TemplateQuery.New(@$"
                UPDATE {tableName}
                SET
                    QueueTTL={futureLock}
                WHERE
                    nID IN ({ids})
            "));
            }
            return rlist;
        }
        private static string? GetHash(string? id)
        {
            if (id == null)
                return id;
            return id.Length > 400 ? id.Substring(0, 400) : id;
        }

        public async Task<EternityEntity?> GetAsync(string? id)
        {
            using var db = await Open();
            var idHash = GetHash(id);
            var query = TemplateQuery.New($"SELECT * FROM EternityEntities WHERE IDHash = {idHash} ID = {id}");
            var result = await db.FromSqlAsync<MySqlEternityEntity>(query);
            return result.FirstOrDefault();
        }

        public async Task SaveAsync(params EternityEntity[] entities)
        {
            var parts = new TemplateFragments(";");
            foreach(var entity in entities)
            {
                var idHash = GetHash(entity.ID);
                var paretIDHash = GetHash(entity.ParentID);
                var q = TemplateQuery.New(@$"INSERT OR REPLACE INTO EternityEntities(
                    ID, Name, Input, IsWorkflow, UtcETA, UtcCreated, UtcUpdated,
                    Response, State, ParentID, Priority
                    
                ) VALUES (
                    {entity.ID},{entity.Name},{entity.Input},{entity.IsWorkflow},
                    {entity.UtcETA.UtcTicks}, {entity.UtcCreated.UtcTicks}, { entity.UtcUpdated.UtcTicks},
                    {entity.Response}, { entity.State}, {entity.ParentID},
                    {entity.Priority}
                )");
                parts.Add(q);
            }

            using var db = await Open();
            await db.ExecuteScalarAsync(parts.ToSqlQuery());
        }

        public Task<string> CreateAsync(EternityEntity entity)
        {
            throw new NotImplementedException();
        }

        public async Task<IAsyncDisposable> LockAsync(EternityEntity entity, TimeSpan maxTTL)
        {
            var start = clock.UtcNow;
            var end = start.Add(maxTTL);
            while(start < end)
            {
                using var db = await Open();
                var q = TemplateQuery.New($"SELECT * FROM ActivityLocks WHERE ID={entity.ID}");
                var l = await db.FromSqlAsync<ActivityLock>(q);
                if (l.Count == 0)
                {
                    await db.ExecuteNonQueryAsync(TemplateQuery.New($"INSERT INTO ActivityLocks(ID, ETA) VALUES ({entity.ID}, {1})"));
                    return new AsyncDisposable(async () => {
                        using var db2 = await Open();
                        await db2.ExecuteNonQueryAsync(TemplateQuery.New($"DELETE FROM ActivityLocks WHERE ID={entity.ID}"));
                    });
                }
                await Task.Delay(this.QueuePollInterval);
                start = start.Add(this.QueuePollInterval);
            }
            throw new TimeoutException();
        }

        public async Task DeleteAsync(EternityEntity entity)
        {
            using var db = await Open();
            await db.ExecuteNonQueryAsync(TemplateQuery.New($"DELETE FROM EternityEntities WHERE ID={entity.ID} OR ParentID={entity.ID}"));
        }

        public async Task DeleteChildrenAsync(EternityEntity entity)
        {
            using var db = await Open();
            await db.ExecuteNonQueryAsync(TemplateQuery.New($"DELETE FROM EternityEntities WHERE ParentID={entity.ID}"));
        }

        public async Task<(EternityEntity? Workflow, EternityEntity? Event)> GetEventAsync(string id, string name, string searchInInput)
        {
            using var db = await Open();
            var query = TemplateQuery.New(@$"
            SELECT * FROM EternityEntities 
                WHERE (ID = {id})
                OR
                (ParentID = {id} AND Name={name} AND (instr(Input, {searchInInput}) > 0))
                ORDER BY IsWorkflow DESC, Priority DESC
                LIMIT 2
            ");

            var list = await db.FromSqlAsync<EternityEntity>(query);
            if (list.Count == 0)
                return (null, null);
            if (list.Count == 1)
                return (list[0], null);
            return (list[0], list[1]);
        }
    }
}
