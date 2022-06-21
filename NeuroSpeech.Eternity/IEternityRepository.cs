using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace NeuroSpeech.Eternity
{

    public class EternityEntity
    {
        internal EternityEntity(string id, string name, params string[] parameters)
        {
            this.ID = id;
            this.Name = name;
            this.Parameters = parameters;
        }

        /// <summary>
        /// Entity Identifier, Must be unique
        /// </summary>
        public string ID { get; set; }

        /// <summary>
        /// Sort Order, in Ascending order, first in first out
        /// </summary>
        public string? SortOrder { get; set; }

        /// <summary>
        /// ClrType of the Root Workflow
        /// or
        /// Clr Method Name of the Workflow Activity
        /// </summary>
        public string Name { get; set; }

        public string[] Parameters { get; set; }

        public DateTime UtcETA { get; set; }

        public DateTime UtcCreated { get; set; }

        public DateTime UtcUpdated { get; set; }

        public string? Response { get; set; }

        public EternityEntityState State { get; set; }

        public string? ParentID { get; set; }

    }

    public enum EternityEntityState
    {
        None,
        Suspended,
        Failed,
        Completed
    }

    public interface IEternityRepository
    {
        Task<List<EternityEntity>> QueryAsync(int max);

        Task<EternityEntity?> GetAsync(string? id);

        Task<EternityEntity> SaveAsync(EternityEntity entity);

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
