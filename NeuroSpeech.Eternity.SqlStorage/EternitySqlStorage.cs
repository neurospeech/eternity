using Microsoft.Data.SqlClient;
using NeuroSpeech.Eternity.Storage;
using System;
using System.Collections.Generic;
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

        public Task<string> CreateAsync(EternityEntity entity)
        {
            throw new NotImplementedException();
        }

        public Task DeleteAsync(EternityEntity entity)
        {
            throw new NotImplementedException();
        }

        public Task DeleteChildrenAsync(EternityEntity entity)
        {
            throw new NotImplementedException();
        }

        public Task<EternityEntity> GetAsync(string id)
        {
            throw new NotImplementedException();
        }

        public Task<IAsyncDisposable> LockAsync(EternityEntity entity, TimeSpan maxTTL)
        {
            throw new NotImplementedException();
        }

        public Task<List<EternityEntity>> QueryAsync(int max, DateTimeOffset utcNow)
        {
            throw new NotImplementedException();
        }

        public Task SaveAsync(params EternityEntity[] entity)
        {
            throw new NotImplementedException();
        }
    }
}
