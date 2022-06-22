using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NeuroSpeech.Eternity
{
    internal struct WaitingTokens
    {
        private ConcurrentDictionary<string, Waiter> tokens;

        public WaitingTokens(int n)
        {
            tokens = new ConcurrentDictionary<string, Waiter>();
        }

        public void Cancel(string tokenKey)
        {
            if (tokens.TryRemove(tokenKey, out var token))
            {
                token.Clear();
            }
        }

        public void Clear()
        {
            foreach (var item in tokens.ToArray())
            {
                item.Value.Clear();
            }
            tokens.Clear();
        }

        public Task Delay(string id, TimeSpan ts, CancellationToken cancellation)
        {
            var token = tokens.GetOrAdd(id, (_) => new Waiter());
            return token.WaitAsync(ts, cancellation);
        }
    }
}
