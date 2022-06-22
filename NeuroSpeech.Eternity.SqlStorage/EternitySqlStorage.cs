using Microsoft.Data.SqlClient;
using NeuroSpeech.Eternity.Storage;
using NeuroSpeech.TemplatedQuery;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NeuroSpeech.Eternity.SqlStorage
{
    public class EternitySqlStorage : IEternityRepository
    {
        private Task? InitAsync;
        private readonly TimeSpan QueuePollInterval;
        private readonly string connectionString;
        private readonly IEternityClock clock;

        public EternitySqlStorage(
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

        public async Task<SqlConnection> Open()
        {
            var conn = new SqlConnection(connectionString);
            if (InitAsync == null)
            {
                InitAsync = ModelCreator.CreateAsync(conn);
            }
            await InitAsync;
            return conn;
        }

        public async Task<List<EternityEntity>> QueryAsync(int max, DateTimeOffset utcNow)
        {
            using var db = await Open();
            var now = utcNow.UtcDateTime;
            var query = TemplateQuery.New($"SELECT TOP ({max}) * FROM EternityEntities WHERE UtcETA < {now} AND IsWorkflow=1 ORDER BY Priority DESC");
            var list = await db.FromSqlAsync<EternityEntity>(query, ignoreUnmatchedProperties: true);
            return list;
        }

        public async Task<EternityEntity?> GetAsync(string? id)
        {
            using var db = await Open();
            var idHash = GetHash(id);
            var query = TemplateQuery.New($"SELECT * FROM EternityEntities WHERE ID = {id} AND IDHash={idHash}");
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
                MERGE EternityEntities as Target 
                USING ( 
                    SELECT * FROM (
                        VALUES (
                            {entity.ID}, {idHash}, {entity.Name},{entity.Input},{entity.IsWorkflow},
                            {entity.UtcETA.UtcDateTime}, {entity.UtcCreated.UtcDateTime}, { entity.UtcUpdated.UtcDateTime},
                            {entity.Response}, { entity.State}, {entity.ParentID}, {paretIDHash},
                            {entity.Priority})
                        ) AS S1(ID, IDHash, Name, Input, IsWorkflow, UtcETA, UtcCreated, UtcUpdated,
                            Response, State, ParentID, ParentIDHash, Priority)
                    ) as S
                ON Target.ID = S.ID AND Target.IDHash = S.IDHash
                WHEN NOT MATCHED BY Target THEN
                    INSERT (ID, IDHash, Name, Input, IsWorkflow, UtcETA, UtcCreated, UtcUpdated,
                    Response, State, ParentID, ParentIDHash, Priority)
                    VALUES (S.ID, S.IDHash, S.Name, S.Input, S.IsWorkflow, S.UtcETA, S.UtcCreated, S.UtcUpdated,
                    S.Response, S.State, S.ParentID, S.ParentIDHash, S.Priority)
                WHEN MATCHED THEN UPDATE SET
                    Target.ID = S.ID,
                    Target.IDHash = S.IDHash,
                    Target.Name = S.Name,
                    Target.Input = S.Input,
                    Target.IsWorkflow = S.IsWorkflow,
                    Target.UtcETA = S.UtcETA,
                    Target.UtcCreated = S.UtcCreated,
                    Target.UtcUpdated = S.UtcUpdated,
                    Target.Response = S.Response,
                    Target.State = S.State,
                    Target.ParentID = S.ParentID,
                    Target.ParentIDHash = S.ParentIDHash,
                    Target.Priority = S.Priority;
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
                    UPDATE EternityEntities
                    SET
                        LockToken={token},
                        LockTTL={lockTTL}
                    WHERE
                        IDHash={idHash} AND ID={id} AND (LockTTL IS NULL OR LockTTL < {now})
");
                int  count = await db.ExecuteNonQueryAsync(update);
                if (count> 0)
                {
                    return new AsyncDisposable(async () => {
                        using var db2 = await Open();
                        var release = TemplateQuery.New(@$"
                        UPDATE EternityEntities
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
            await db.ExecuteNonQueryAsync(TemplateQuery.New(@$"DELETE FROM EternityEntities WHERE 
                (ID={entity.ID} AND IDHash={idHash}) 
                OR (ParentID={entity.ID} AND ParentIDHash={idHash})"));
        }

        public async Task DeleteChildrenAsync(EternityEntity entity)
        {
            using var db = await Open();
            var idHash = GetHash(entity.ID);
            await db.ExecuteNonQueryAsync(TemplateQuery.New(@$"DELETE FROM EternityEntities 
                WHERE ParentID={entity.ID}
                AND ParentIDHash={idHash}"));
        }

        public async Task<(EternityEntity? Workflow, EternityEntity? Event)> GetEventAsync(string id, string name, string searchInInput)
        {
            using var db = await Open();
            var idHash = GetHash(id);
            var query = TemplateQuery.New(@$"
            SELECT TOP 2 * FROM EternityEntities 
                WHERE (ID = {id} AND IDHash={idHash})
                OR
                (ParentID = {id} AND ParentIDHash = {idHash} AND CHARINDEX(Input, {searchInInput})> 0)
                ORDER BY IsWorkflow DESC, Priority DESC
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
