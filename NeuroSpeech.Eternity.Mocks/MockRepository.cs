using NeuroSpeech.Eternity.Storage;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NeuroSpeech.Eternity
{
    internal class MockAsyncLock : IAsyncDisposable
    {
        private ConcurrentDictionary<string, MockAsyncLock> locks;
        private string ID;

        public MockAsyncLock()
        {

        }

        public MockAsyncLock(ConcurrentDictionary<string, MockAsyncLock> locks, string iD)
        {
            this.locks = locks;
            this.ID = iD;
        }

        public ValueTask DisposeAsync()
        {
            locks.TryRemove(ID, out var _);
            return new ValueTask();
        }
    }

    public class MockRepository : IEternityRepository
    {

        private ConcurrentDictionary<string, EternityEntity> db = new ConcurrentDictionary<string, EternityEntity>();

        private ConcurrentDictionary<string, MockAsyncLock> locks = new ConcurrentDictionary<string, MockAsyncLock>();

        public async Task<string> CreateAsync(EternityEntity entity)
        {
            await Task.Delay(10);
            db.AddOrUpdate(entity.ID, entity, (key, old) => entity);
            return entity.ID;
        }

        public async Task DeleteAsync(EternityEntity entity)
        {
            await Task.Delay(10);
            db.TryRemove(entity.ID, out var id);
        }

        public async Task DeleteChildrenAsync(EternityEntity entity)
        {
            await Task.Delay(10);
            foreach(var item in db.ToList())
            {
                if(item.Value.ParentID == entity.ID)
                {
                    db.TryRemove(item.Key, out var i);
                }
            }
        }

        public async Task<EternityEntity> GetAsync(string id)
        {
            await Task.Delay(10);
            if (db.TryGetValue(id, out var result))
                return result;
            foreach(var item in db)
            {
                if(item.Key == id || item.Value.ID == id)
                {
                    return item.Value;
                }
            }
            return null;
        }

        public async Task<IAsyncDisposable> LockAsync(EternityEntity entity, TimeSpan maxTTL)
        {
            await Task.Delay(10);
            MockAsyncLock m;
            while(locks.TryGetValue(entity.ID, out m))
            {
                await Task.Delay(maxTTL);
            }
            if (locks.TryGetValue(entity.ID, out m))
                throw new TimeoutException($"Failed to lock {entity.ID}");
            m = new MockAsyncLock(locks, entity.ID);
            locks.AddOrUpdate(entity.ID, m, (_, _) => m);
            return m;
        }

        public async Task<List<EternityEntity>> QueryAsync(int max, DateTimeOffset utcNow)
        {
            await Task.Delay(10);
            var result = new List<EternityEntity>();
            foreach(var item in db.ToList())
            {
                if (item.Value.IsWorkflow && item.Value.UtcETA <= utcNow)
                {
                    result.Add(item.Value);
                }
            }
            return result.OrderByDescending(x => x.Priority).Take(max).ToList();
        }

        public async Task SaveAsync(params EternityEntity[] entity)
        {
            await Task.Delay(10);
            foreach(var item in entity)
            {
                db.AddOrUpdate(item.ID, item, (_, _) => item);
            }
        }
    }
}
