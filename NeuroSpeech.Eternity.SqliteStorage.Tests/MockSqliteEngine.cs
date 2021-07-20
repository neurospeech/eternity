using Microsoft.Extensions.DependencyInjection;
using NeuroSpeech.Eternity.Mocks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NeuroSpeech.Eternity.SqliteStorage.Tests
{
    public class MockSqliteEngine : MockEngine<EternitySqliteStorage>
    {

        private string? filePath;

        public MockSqliteEngine(Action<IServiceCollection>? builder = null) : base(builder)
        {
        }

        private static long dbID = 1;

        protected override EternitySqliteStorage CreateStorage(MockClock clock)
        {
            this.filePath = $"{System.IO.Path.GetTempFileName()}-{Interlocked.Increment(ref dbID)}.db3";
            var cb = new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder();
            cb.DataSource = filePath;
            return new EternitySqliteStorage(cb.ToString(), clock, TimeSpan.FromMilliseconds(100));
        }

        public override void Dispose()
        {
            if (filePath != null)
            {
                try
                {
                    // System.IO.File.Delete(filePath);
                    System.Diagnostics.Debug.WriteLine(filePath);
                }
                catch { }
                filePath = null;
            }
        }
    }
}
