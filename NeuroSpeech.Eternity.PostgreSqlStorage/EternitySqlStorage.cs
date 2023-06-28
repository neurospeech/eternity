using NeuroSpeech.Eternity.Storage;
using NeuroSpeech.TemplatedQuery;
using Npgsql;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NeuroSpeech.Eternity.PostgreSqlStorage
{
    public class SqlEternityEntity: EternityEntity
    {
        public long NID { get; set; }
    }

    public class EternityPostgreSqlStorage : IEternityRepository
    {
        private Task? InitAsync;
        private readonly TimeSpan QueuePollInterval;
        private readonly string connectionString;
        private readonly IEternityClock clock;
        private readonly Literal schemaName;
        private readonly Literal tableName;

        public EternityPostgreSqlStorage(
            string connectionString,
            IEternityClock clock,
            string tableName = "EternityEntities",
            string schemaName = "dbo",
            TimeSpan queuePollInterval = default)
        {
            this.QueuePollInterval = queuePollInterval.Ticks == 0
                ? TimeSpan.FromSeconds(15)
                : queuePollInterval;
            this.connectionString = connectionString;
            this.clock = clock;
            this.schemaName = new Literal(schemaName);
            this.tableName = new Literal(tableName);
        }

        public async Task<NpgsqlConnection> Open()
        {
            var conn = new NpgsqlConnection(connectionString);
            if (InitAsync == null)
            {
                InitAsync = ModelCreator.CreateAsync(conn, schemaName, tableName);
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
                SELECT TOP ({max}) *
                FROM ""{schemaName}"".""{tableName}""
                WHERE 
                    ""UtcETA"" < {now}
                    AND ""IsWorkflow""=1
                    AND (""QueueTTL"" IS NULL OR ""QueueTTL""<{now})
                ORDER BY ""Priority"" DESC");
            var list = await db.FromSqlAsync<SqlEternityEntity>(query, ignoreUnmatchedProperties: true);
            var rlist = new List<EternityEntity>();
            var ids = new List<long>();
            foreach(var item in list)
            {
                rlist.Add(item);
                ids.Add(item.NID);
            }
            if (ids.Any())
            {
                await db.ExecuteNonQueryAsync(TemplateQuery.New(@$"
                UPDATE ""{schemaName}"".""{tableName}""
                SET
                    ""QueueTTL""={futureLock}
                WHERE
                    ""nID"" IN ({ids})
            "));
            }
            return rlist;
        }

        public async Task<EternityEntity?> GetAsync(string? id)
        {
            using var db = await Open();
            var idHash = GetHash(id);
            var query = TemplateQuery.New(@$"SELECT * FROM ""{schemaName}"".""{tableName}"" WHERE ""ID"" = {id} AND ""IDHash""={idHash}");
            var result = await db.FromSqlAsync<EternityEntity>(query, ignoreUnmatchedProperties: true);
            return result.FirstOrDefault();
        }

        private static string? GetHash(string? id)
        {
            if (id == null)
                return id;
            return id.Length > 400 ? id.Substring(0, 400) : id;
        }

        public async Task SaveAsync(params EternityEntity[] entities)
        {
            var parts = new TemplateFragments("");
            foreach (var entity in entities)
            {
                var idHash = GetHash(entity.ID);
                var paretIDHash = GetHash(entity.ParentID);
                var q = TemplateQuery.New(@$"
                MERGE ""{schemaName}"".""{tableName}"" as Target 
                USING ( 
                    SELECT * FROM (
                        VALUES (
                            {entity.ID}, {idHash}, {entity.Name},{entity.Input},{entity.IsWorkflow},
                            {entity.UtcETA.UtcDateTime}, {entity.UtcCreated.UtcDateTime}, { entity.UtcUpdated.UtcDateTime},
                            {entity.Response}, { entity.State}, {entity.ParentID}, {paretIDHash},
                            {entity.Priority}, {entity.Extra})
                        ) AS S1(ID, IDHash, Name, Input, IsWorkflow, UtcETA, UtcCreated, UtcUpdated,
                            Response, State, ParentID, ParentIDHash, Priority, Extra)
                    ) as S
                ON Target.ID = S.ID AND Target.IDHash = S.IDHash
                WHEN NOT MATCHED BY Target THEN
                    INSERT (ID, IDHash, Name, Input, IsWorkflow, UtcETA, UtcCreated, UtcUpdated,
                    Response, State, ParentID, ParentIDHash, Priority, Extra)
                    VALUES (S.ID, S.IDHash, S.Name, S.Input, S.IsWorkflow, S.UtcETA, S.UtcCreated, S.UtcUpdated,
                    S.Response, S.State, S.ParentID, S.ParentIDHash, S.Priority, S.Extra)
                WHEN MATCHED THEN UPDATE SET
                    Target.ID = S.ID,
                    Target.IDHash = S.IDHash,
                    Target.Name = S.Name,
                    Target.Input = S.Input,
                    Target.IsWorkflow = S.IsWorkflow,
                    Target.QueueTTL = CASE WHEN S.UtcETA <> Target.UtcETA THEN NULL ELSE Target.QueueTTL END,
                    Target.UtcETA = S.UtcETA,
                    Target.UtcCreated = S.UtcCreated,
                    Target.UtcUpdated = S.UtcUpdated,
                    Target.Response = S.Response,
                    Target.State = S.State,
                    Target.ParentID = S.ParentID,
                    Target.ParentIDHash = S.ParentIDHash,
                    Target.Priority = S.Priority,
                    Target.Extra = S.Extra;
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
                    UPDATE ""{schemaName}"".""{tableName}""
                    SET
                        ""LockToken""={token},
                        ""LockTTL""={lockTTL}
                    WHERE
                        ""IDHash""={idHash} AND ""ID""={id} AND (""LockTTL"" IS NULL OR ""LockTTL"" < {now})
");
                int  count = await db.ExecuteNonQueryAsync(update);
                if (count> 0)
                {
                    return new AsyncDisposable(async () => {
                        using var db2 = await Open();
                        var release = TemplateQuery.New(@$"
                        UPDATE ""{schemaName}"".""{tableName}""
                        SET
                            ""LockToken"" = NULL,
                            ""LockTTL"" = NULL
                        WHERE
                            ""IDHash""={idHash} AND ""ID""={id} AND ""LockToken""={token}
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

            // delete 100s of items for 15 seconds and yield...
            var start = DateTime.UtcNow;
            while (true)
            {
                int step = await db.ExecuteNonQueryAsync(TemplateQuery.New(@$"DELETE TOP (100) FROM ""{schemaName}"".""{tableName}"" WHERE 
                (""ParentID""={entity.ID} AND ""ParentIDHash""={idHash})"));
                if (step == 0)
                {
                    break;
                }
                var diff = DateTime.UtcNow - start;
                if(diff.TotalSeconds > 15)
                {
                    return;
                }
            }


            await db.ExecuteNonQueryAsync(TemplateQuery.New(@$"DELETE FROM ""{schemaName}"".""{tableName}"" WHERE 
                (""ID""={entity.ID} AND ""IDHash""={idHash}) 
                OR (""ParentID""={entity.ID} AND ""ParentIDHash""={idHash})"));
        }

        public async Task DeleteChildrenAsync(EternityEntity entity)
        {
            using var db = await Open();
            var idHash = GetHash(entity.ID);
            await db.ExecuteNonQueryAsync(TemplateQuery.New(@$"DELETE FROM ""{schemaName}"".""{tableName}"" 
                WHERE ""ParentID""={entity.ID}
                AND ""ParentIDHash""={idHash}"));
        }

        public async Task<(EternityEntity? Workflow, EternityEntity? Event)> GetEventAsync(string id, string name, string searchInInput)
        {
            using var db = await Open();
            var idHash = GetHash(id);
            var query = TemplateQuery.New(@$"
            SELECT TOP 2 * FROM ""{schemaName}"".""{tableName}"" 
                WHERE (""ID"" = {id} AND ""IDHash""={idHash})
                OR
                (""ParentID"" = {id} AND ""ParentIDHash"" = {idHash}
                    AND Name = {name}
                    AND (CHARINDEX({searchInInput}, ""Input"")> 0))
                ORDER BY ""IsWorkflow"" DESC, ""Priority"" DESC
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
