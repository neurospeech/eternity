using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NeuroSpeech.Eternity.Mocks
{

    public class MockLock: IEternityLock {
        public readonly string id;

        public MockLock(string id)
        {
            this.id = id;
        }
    
    }

    public class MockQueueItem: WorkflowQueueItem
    {
    }

    public class MockStorage : IEternityStorage
    {
        private readonly IEternityClock clock;
        private ConcurrentDictionary<string, IEternityLock> locks = new ConcurrentDictionary<string, IEternityLock>();

        private List<ActivityStep> list = new List<ActivityStep>();
        private List<WorkflowStep> workflows = new List<WorkflowStep>();
        private List<MockQueueItem> queue = new List<MockQueueItem>();


        public MockStorage(IEternityClock clock)
        {
            this.clock = clock;
        }

        public int QueueSize => queue.Count;

        public async Task<IEternityLock> AcquireLockAsync(string id, long sequenceId)
        {
            var key = $"{id}-{sequenceId}";
            var newLock = new MockLock(key);
            while(true)
            {
                if(locks.TryAdd(key, newLock))
                {
                    return newLock;
                }
                await Task.Delay(100);
                continue;
            }
        }

        public Task DeleteHistoryAsync(string id)
        {
            list.RemoveAll(x => x.ID == id);
            return Task.CompletedTask;
        }

        public Task DeleteWorkflowAsync(string id)
        {
            list.RemoveAll(x => x.ID == id);
            queue.RemoveAll(x => x.ID == id);
            workflows.RemoveAll(x => x.ID == id);
            return Task.CompletedTask;
        }

        public Task FreeLockAsync(IEternityLock executionLock)
        {
            locks.TryRemove((executionLock as MockLock).id, out var n);
            return Task.CompletedTask;
        }

        public Task<ActivityStep> GetEventAsync(string id, string eventName)
        {
            var e = list.FirstOrDefault(x => x.ID == id
                && x.ActivityType == ActivityType.Event
                && x.Status != ActivityStatus.Completed
                && x.Status != ActivityStatus.Failed);
            return Task.FromResult(e);
        }

        public Task<WorkflowQueueItem[]> GetScheduledActivitiesAsync(int maxActivitiesToProcess)
        {
            var pending = queue
                .Where(x => x.ETA <= clock.UtcNow)
                .OfType<WorkflowQueueItem>()
                .Take(maxActivitiesToProcess)
                .ToArray();
            return Task.FromResult(pending);
        }

        public Task<ActivityStep> GetStatusAsync(ActivityStep key)
        {
            var item = list.FirstOrDefault(x => x.ID == key.ID
            && x.ActivityType == key.ActivityType
            && x.Key == key.Key);
            return Task.FromResult(item);
        }

        public Task<WorkflowStep> GetWorkflowAsync(string id)
        {
            return Task.FromResult(workflows.FirstOrDefault(x =>x.ID == id));
        }

        public Task<ActivityStep> InsertActivityAsync(ActivityStep key)
        {
            list.Add(key);
            key.SequenceID = list.Count;
            return Task.FromResult(key);
        }

        public Task<WorkflowStep> InsertWorkflowAsync(WorkflowStep step)
        {
            if(string.IsNullOrEmpty(step.ID))
            {
                step.ID = Guid.NewGuid().ToString("N");
            } else
            {
                if (workflows.Any(x => x.ID == step.ID))
                    throw new InvalidOperationException();
            }
            workflows.Add(step);
            return Task.FromResult(step);
        }

        public Task<string> QueueWorkflowAsync(WorkflowQueueItem item, string existing = null)
        {
            if(existing != null)
            {
                var e = queue.FirstOrDefault(x => x.QueueToken == existing);
                e.ETA = item.ETA;
                e.ID = item.ID;
                e.Command = item.Command;
                return Task.FromResult(existing);
            }
            var qt = new MockQueueItem {
                ID = item.ID,
                ETA = item.ETA ,
                Command = item.Command,
                QueueToken = Guid.NewGuid().ToString("N") };
            queue.Add(qt);
            return Task.FromResult<string>(qt.QueueToken);
        }

        public Task RemoveQueueAsync(params string[] tokens)
        {
            foreach (var token in tokens)
            {
                queue.RemoveAll(x => x.QueueToken == token);
            }
            return Task.CompletedTask;
        }

        public Task UpdateAsync(ActivityStep key)
        {
            list[(int)key.SequenceID - 1] = key;
            return Task.CompletedTask;
        }

        public Task UpdateAsync(WorkflowStep key)
        {
            int index = workflows.FindIndex(x => x.ID == key.ID);
            workflows[index] = key;
            return Task.CompletedTask;
        }
    }

    public class QueueToken: IQueueToken
    {
        public readonly string ID;

        public readonly DateTimeOffset ETA;

        public QueueToken(string id, DateTimeOffset eta)
        {
            this.ID = id;
            this.ETA = eta;
        }
    }
}
