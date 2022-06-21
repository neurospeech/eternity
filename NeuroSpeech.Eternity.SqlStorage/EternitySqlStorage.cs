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
            var query = TemplateQuery.New($"SELECT * FROM EternityEntities WHERE UtcETA < {utcNow.UtcTicks} AND IsWorkflow=1 ORDER BY Priority DESC LIMIT {max}");
            return await db.FromSqlAsync<EternityEntity>(query);
        }

        public async Task<EternityEntity?> GetAsync(string? id)
        {
            using var db = await Open();
            var idHash = GetHash(id);
            var query = TemplateQuery.New($"SELECT * FROM EternityEntities WHERE ID = {id} AND IDHash={idHash}");
            var result = await db.FromSqlAsync<EternityEntity>(query);
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
            var parts = new TemplateFragments(";");
            foreach (var entity in entities)
            {
                var idHash = GetHash(entity.ID);
                var paretIDHash = GetHash(entity.ParentID);
                var q = TemplateQuery.New(@$"MERGE EternityEntities as Target 
                USING (
                VALUES (
                    {entity.ID}, {idHash}, {entity.Name},{entity.Input},{entity.IsWorkflow},
                    {entity.UtcETA.UtcTicks}, {entity.UtcCreated.UtcTicks}, { entity.UtcUpdated.UtcTicks},
                    {entity.Response}, { entity.State}, {entity.ParentID}, {paretIDHash},
                    {entity.Priority}, {entity.CurrentWaitingID}
                ) AS S(ID, IDHash, Name, Input, IsWorkflow, UtcETA, UtcCreated, UtcUpdated,
                    Response, State, ParentID, ParentIDHash, Priority, CurrentWaitingID)

                ON Target.ID = S.ID
                WHEN NOT MATCHED BY Target
                    INSERT (ID, IDHash, Name, Input, IsWorkflow, UtcETA, UtcCreated, UtcUpdated,
                    Response, State, ParentID, ParentIDHash, Priority, CurrentWaitingID)
                    VALUES (S.ID, S.IDHash, S.Name, S.Input, S.IsWorkflow, S.UtcETA, S.UtcCreated, S.UtcUpdated,
                    S.Response, S.State, S.ParentID, S.ParentIDHash, S.Priority, S.CurrentWaitingID)
                WHEM MATCHED THEM UPDATE SET
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
                    Target.Priority = S.Priority,
                    Target.CurrentWaitingID = S.CurrentWaitingID
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
            var start = clock.UtcNow;
            var end = start.Add(maxTTL);
            while (start < end)
            {
                using var db = await Open();
                var q = TemplateQuery.New($"SELECT * FROM ActivityLocks WHERE ID={entity.ID}");
                var l = await db.FromSqlAsync<ActivityLock>(q);
                if (l.Count == 0)
                {
                    await db.ExecuteNonQueryAsync(TemplateQuery.New($"INSERT INTO ActivityLocks(ID) VALUES ({entity.ID})"));
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
    }
}
