using System;
using System.Threading.Tasks;

namespace NeuroSpeech.Eternity
{
    public class AsyncDisposable : IAsyncDisposable
    {
        private readonly Func<Task> task;

        public AsyncDisposable(Func<Task> task)
        {
            this.task = task;
        }

        public ValueTask DisposeAsync()
        {
            return new ValueTask(task());
        }
    }
}
