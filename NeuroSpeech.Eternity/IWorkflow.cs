using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace NeuroSpeech.Eternity
{
    internal interface IWorkflow
    {
        void Init(string id, EternityContext context, DateTimeOffset start, bool generated);
        void SetCurrentTime(DateTimeOffset time);

        Type InputType { get; }

        DateTimeOffset CurrentUtc { get; }

        Task<object> RunAsync(object input);
        
        IList<string> QueueItemList { get; }

        bool  IsActivityRunning { get; set; }

        bool IsGenerated { get; }

        bool DeleteHistory { get; }

        string ID { get; }
    }
}
