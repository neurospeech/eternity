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
        }

        public Task WaitAsync(
            TimeSpan ts,
            CancellationToken cancelToken = default)
        {
            if (ts.TotalMilliseconds <= 0)
                return Task.CompletedTask;
            return WaitAsync((int)ts.TotalMilliseconds, cancelToken);
        }

        public Task WaitAsync(
            int milliSeconds,
            CancellationToken cancelToken = default) {
            var t = new TaskCompletionSource<int>();
            var ct = trigger = new CancellationTokenSource(milliSeconds);
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
