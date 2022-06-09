using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace NeuroSpeech.Eternity
{
    internal struct WaitingTokens
    {
        private ConcurrentDictionary<string, CancellationTokenSource> tokens;

        public WaitingTokens(int n)
        {
            tokens = new ConcurrentDictionary<string, CancellationTokenSource>();
        }

        public CancellationToken Get(string tokenKey)
        {
            var token = tokens.AddOrUpdate(tokenKey,
                (x) => new CancellationTokenSource(),
                (k, update) => new CancellationTokenSource());
            return token.Token;
        }

        public void Cancel(string tokenKey)
        {
            if (tokens.TryRemove(tokenKey, out var token))
            {
                if (!token.IsCancellationRequested)
                    token.Cancel();
            }
        }

        public void Clear()
        {
            foreach (var item in tokens.ToArray())
            {
                var c = item.Value;
                if (!c.IsCancellationRequested)
                {
                    c.Cancel();
                }
            }
        }
    }
}
