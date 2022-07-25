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
        private readonly Literal tableName;

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
            this.tableName = new Literal(tableName);
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
            var query = TemplateQuery.New($"SELECT * FROM {tableName} WHERE IDHash = {idHash} AND ID = {id}");
            var result = await db.FromSqlAsync<MySqlEternityEntity>(query);
            return result.FirstOrDefault();
        }

        public async Task SaveAsync(params EternityEntity[] entities)
        {
            var parts = new TemplateFragments(";");
            foreach(var entity in entities)
            {
                var id = entity.ID;
                var idHash = GetHash(id);
                var paretIDHash = GetHash(entity.ParentID);
                var q = TemplateQuery.New(@$"
    CALL Save{tableName}(
            {entity.ID}, {idHash},{entity.Name},{entity.Input},{entity.IsWorkflow},
            {entity.UtcETA.UtcTicks}, {entity.UtcCreated.UtcTicks}, {entity.UtcUpdated.UtcTicks},
            {entity.Response}, {entity.State}, {entity.ParentID}, {paretIDHash},
            {entity.Priority})
    
");
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
            var now = clock.UtcNow.UtcDateTime;
            var start = now;
            var end = start.Add(maxTTL);
            var token = Guid.NewGuid().ToString("N");
            var id = entity.ID;
            var idHash = GetHash(id);
            var lockTTL = now.AddMinutes(1);
            while (start < end)
            {
                using var db = await Open();
                var update = TemplateQuery.New(@$"
                    UPDATE {tableName}
                    SET
                        LockToken={token},
                        LockTTL={lockTTL}
                    WHERE
                        IDHash={idHash} AND ID={id} AND (LockTTL IS NULL OR LockTTL < {now})
");
                int count = await db.ExecuteNonQueryAsync(update);
                if (count > 0)
                {
                    return new AsyncDisposable(async () => {
                        using var db2 = await Open();
                        var release = TemplateQuery.New(@$"
                        UPDATE {tableName}
                        SET
                            LockToken = NULL,
                            LockTTL = NULL
                        WHERE
                            IDHash={idHash} AND ID={id} AND LockToken={token}
");
                        await db2.ExecuteNonQueryAsync(release);
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
            var idHash = GetHash(entity.ID);
            var parentIDHash = GetHash(entity.ParentID);
            await db.ExecuteNonQueryAsync(TemplateQuery.New(@$"
                DELETE FROM {tableName}
                WHERE (IDHash={idHash} AND ID={entity.ID})
                    OR (ParentIDHash={parentIDHash} AND ParentID={entity.ID})
            "));
        }

        public async Task DeleteChildrenAsync(EternityEntity entity)
        {
            using var db = await Open();
            var idHash = GetHash(entity.ID);
            await db.ExecuteNonQueryAsync(TemplateQuery.New(@$"
                DELETE FROM {tableName}
                WHERE (ParentIDHash={idHash} AND ParentID={entity.ID})
            "));
        }

        public async Task<(EternityEntity? Workflow, EternityEntity? Event)> GetEventAsync(string id, string name, string searchInInput)
        {
            using var db = await Open();
            var idHash = GetHash(id);
            var query = TemplateQuery.New(@$"
            SELECT * FROM {tableName} 
                WHERE (ID = {id} AND IDHash={idHash})
                OR
                (ParentID = {id} AND ParentIDHash = {idHash}
                    AND Name = {name}
                    AND (CHARINDEX({searchInInput}, Input)> 0))
                ORDER BY IsWorkflow DESC, Priority DESC
                LIMIT 2
            ");

            var list = await db.FromSqlAsync<EternityEntity>(query, ignoreUnmatchedProperties: true);
            if (list.Count == 0)
                return (null, null);
            if (list.Count == 1)
                return (list[0], null);
            return (list[0], list[1]);
        }
    }
}
