using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace NeuroSpeech.Eternity.Storage
{

    public enum EternityEntityState
    {
        None,
        Suspended,
        Failed,
        Completed
    }

    public interface IEternityRepository
    {
        Task<List<EternityEntity>> QueryAsync(int max, DateTimeOffset utcNow);

        Task<EternityEntity?> GetAsync(string? id);

        Task<(EternityEntity? Workflow, EternityEntity? Event)> GetEventAsync(string id, string name, string searchInInput);

        Task SaveAsync(params EternityEntity[] entity);

        Task<string> CreateAsync(EternityEntity entity);

        Task<IAsyncDisposable> LockAsync(EternityEntity entity, TimeSpan maxTTL);

        /// <summary>
        /// Deletes the entity and all its children
        /// </summary>
        /// <param name="entity"></param>
        /// <returns></returns>
        Task DeleteAsync(EternityEntity entity);

        /// <summary>
        /// Deletes all children of given entity
        /// </summary>
        /// <param name="entity"></param>
        /// <returns></returns>
        Task DeleteChildrenAsync(EternityEntity entity);
    }
}
