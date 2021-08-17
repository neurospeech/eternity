using System;
using System.Threading.Tasks;

namespace NeuroSpeech.Eternity
{

    public interface IQueueToken
    {

    }

    public interface IEternityStorage
    {
        Task<IEternityLock> AcquireLockAsync(string id, long sequenceId);
        Task FreeLockAsync(IEternityLock executionLock);
        Task<ActivityStep> GetStatusAsync(ActivityStep key);

        Task<WorkflowStep> GetWorkflowAsync(string id);

        /// <summary>
        /// Insert the workflow, if ID already exists in the system, throw an error
        /// </summary>
        /// <param name="step"></param>
        /// <returns></returns>
        Task<WorkflowStep> InsertWorkflowAsync(WorkflowStep step);

        /// <summary>
        /// Queue the workflow id if it is not completed or failed, otherwise do not do anything and return null
        /// </summary>
        /// <param name="step"></param>
        /// <param name="after"></param>
        /// <returns></returns>
        Task<string> QueueWorkflowAsync(WorkflowQueueItem item, string? replace = null);

        Task RemoveQueueAsync(params string[] token);

        Task<ActivityStep> InsertActivityAsync(ActivityStep key);

        Task<WorkflowQueueItem[]> GetScheduledActivitiesAsync(int maxActivitiesToProcess = 100);

        Task UpdateAsync(ActivityStep key);

        Task UpdateAsync(WorkflowStep key);

        /// <summary>
        /// Return not completed/not failed waiting event 
        /// </summary>
        /// <param name="id"></param>
        /// <param name="eventName"></param>
        /// <returns></returns>
        Task<ActivityStep> GetEventAsync(string id, string eventName);
        
        /// <summary>
        /// Delete history of the specified workflow
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        Task DeleteHistoryAsync(string id);

        /// <summary>
        /// Deletes the workflow along with the history
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        Task DeleteWorkflowAsync(string id);
    }

}
