using Microsoft.Extensions.DependencyInjection;
using MySql.Data.MySqlClient;
using MySql.Server;
using NeuroSpeech.Eternity.Mocks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NeuroSpeech.Eternity.SqliteStorage.Tests
{
    public class MockMySqlStorage : MockEngine<EternityMySqlStorage>
    {

        private string? filePath;
        private string ConnectionString;

        public MockMySqlStorage(Action<IServiceCollection>? builder = null) : base(builder)
        {

        }

        // private static long dbID = 1;

        protected override EternityMySqlStorage CreateStorage(MockClock clock)
        {
            MySqlServer.Instance.StartServer();
            this.ConnectionString = MySqlServer.Instance.GetConnectionString();
            CreateDatabase("db1");
            return new EternityMySqlStorage(
                MySqlServer.Instance.GetConnectionString("db1"), clock, "Workflows", TimeSpan.FromMilliseconds(100));
        }

        public override void Dispose()
        {
            MySqlServer.Instance.ShutDown();
        }


        private void CreateDatabase(string DBName)
        {
            Execute($"CREATE DATABASE {DBName}");
        }

        private void Execute(string command, string? db = null)
        {
            using (var c = new MySqlConnection(ConnectionString))
            {
                if (db != null)
                {
                    c.ConnectionString += ";database=" + db;
                }
                c.Open();


                using (var cmd = c.CreateCommand())
                {
                    cmd.CommandText = command;
                    cmd.ExecuteNonQuery();
                }
            }

        }

        private void DeleteDatabase(string DBName)
        {
            Execute($"DROP DATABASE [{DBName}]");
        }
    }
}
