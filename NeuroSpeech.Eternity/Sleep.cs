using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NeuroSpeech.Eternity
{
    internal class Trigger
    {

    }

    internal class Waiter
    {

        private CancellationTokenSource? trigger;

        public void Clear()
        {
            trigger?.Cancel();
            trigger = null;
        }

        public Task WaitAsync(
            TimeSpan ts,
            CancellationToken cancelToken = default)
        {
            if (ts.TotalMilliseconds <= 0)
                return Task.CompletedTask;
            var t = new TaskCompletionSource<int>();
            var ct = trigger = new CancellationTokenSource(ts);
            ct.Token.Register(() => {
                t.TrySetResult(1);
            });
            cancelToken.Register(() => {
                t.TrySetCanceled();
            });

            return t.Task;
        }

    }
}
