using NeuroSpeech.Eternity.Storage;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace NeuroSpeech.Eternity
{

    internal interface IWorkflow
    {
        EternityContext Context { get; }

        EternityEntity Entity { get; set; }

        void Init(string id, EternityContext context, DateTimeOffset start, bool generated, Type originalType);
        void SetCurrentTime(DateTimeOffset time);

        Type InputType { get; }

        DateTimeOffset CurrentUtc { get; }

        Task<object> RunAsync(object input);
        
        bool  IsActivityRunning { get; set; }

        bool IsGenerated { get; }

        bool DeleteHistory { get; }

        string ID { get; }

        TimeSpan PreserveTime { get; }

        TimeSpan FailurePreserveTime { get;}

        int WaitCount { get; set; }

        int Priority { get; set; }

        IDictionary<string,string> Extra { get; }

        Task SaveAsync();
    }
}
