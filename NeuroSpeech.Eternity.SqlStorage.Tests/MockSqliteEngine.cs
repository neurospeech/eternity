using Microsoft.Extensions.DependencyInjection;
using NeuroSpeech.Eternity.Mocks;
using NeuroSpeech.Eternity.SqlStorage;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NeuroSpeech.Eternity.SqliteStorage.Tests
{
    public class MockSqliteEngine : MockEngine<EternitySqlStorage>
    {

        private string? filePath;

        public MockSqliteEngine(Action<IServiceCollection>? builder = null) : base(builder)
        {
        }

        private static long dbID = 1;

        protected override EternitySqlStorage CreateStorage(MockClock clock)
        {
            this.filePath = $"{System.IO.Path.GetTempFileName()}-{Interlocked.Increment(ref dbID)}";
            var cb = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder();
            cb.DataSource = filePath;
            return new EternitySqlStorage(cb.ToString(), clock, TimeSpan.FromMilliseconds(100));
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
