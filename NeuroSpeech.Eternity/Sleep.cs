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
        private TimeSpan? lastCancelAfter;

        public void Clear()
        {
            trigger?.Cancel();
            trigger = null;
        }

        public void ClearAfter(TimeSpan after)
        {
            var last = lastCancelAfter;
            if (last != null)
            {
                if(last.Value.TotalMilliseconds < after.TotalMilliseconds)
                {
                    return;
                }
            }
            lastCancelAfter = after;
            trigger?.CancelAfter(after);
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
            lastCancelAfter = TimeSpan.FromMilliseconds(milliSeconds);
            var ct = trigger = new CancellationTokenSource(milliSeconds);
            ct.Token.Register(() => {
                t.TrySetResult(1);
                lastCancelAfter = null;
            });
            cancelToken.Register(() => {
                t.TrySetCanceled();
                lastCancelAfter = null;
            });

            return t.Task;
        }

    }
}
