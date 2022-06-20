using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace NeuroSpeech.Eternity
{
    public class EternityContext2
    {
        private readonly IEternityClock clock;

        public EternityContext2(IEternityClock clock)
        {
            this.clock = clock;
        }

        internal Task CreateAsync(IWorkflow workflow)
        {
            throw new Exception();
        }

        
    }

    public interface IEternityEntity
    {
        /// <summary>
        /// Entity Identifier, Must be unique
        /// </summary>
        public string ID { get; set; }

        /// <summary>
        /// Sort Order, in Ascending order, first in first out
        /// </summary>
        public string SortOrder { get; set; }

        public TimeSpan? KeepAlive { get; set; }

        public TimeSpan? KeepAliveWhenFailed { get; set; }

        /// <summary>
        /// ClrType of the Root Workflow
        /// or
        /// Clr Method Name of the Workflow Activity
        /// </summary>
        public string Name { get; set; }

        public object[] Parameters { get; set; }

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
        Failed,
        Completed
    }

    public interface IEternityRepository
    {


        Task<IEternityEntity> GetAsync(string? id);

        Task<IEternityEntity> SaveAsync(IEternityEntity entity);

        Task LockAsync(IEternityEntity entity, TimeSpan maxTTL);

        /// <summary>
        /// Deletes the entity and all its children
        /// </summary>
        /// <param name="entity"></param>
        /// <returns></returns>
        Task DeleteAsync(IEternityEntity entity);

        /// <summary>
        /// Deletes all children of given entity
        /// </summary>
        /// <param name="entity"></param>
        /// <returns></returns>
        Task DeleteChildren(IEternityEntity entity);
    }
}
