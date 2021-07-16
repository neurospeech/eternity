using System;
using System.Threading.Tasks;

namespace NeuroSpeech.Eternity
{
    public interface IWorkflowObject
    {
        Task<T> InternalScheduleResultAsync<T>(string name, params object?[] parameters);
        Task InternalScheduleAsync(string method, params object?[] items);

        Task<T> InternalScheduleAtResultAsync<T>(DateTimeOffset at, string method, params object?[] items);
        Task InternalScheduleAtAsync(DateTimeOffset at, string method, params object?[] items);

        Task<T> InternalScheduleAfterResultAsync<T>(TimeSpan at, string method, params object?[] items);

        Task InternalScheduleAfterAsync(TimeSpan at, string method, params object?[] items);
    }
}
