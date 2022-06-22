using Microsoft.Data.SqlClient;
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
    public class MockSqlStrage : MockEngine<EternitySqlStorage>
    {

        private string? filePath;
        private string DBName;

        public MockSqlStrage(Action<IServiceCollection>? builder = null) : base(builder)
        {
        }

        protected override EternitySqlStorage CreateStorage(MockClock clock)
        {

            this.DBName = "App" + Guid.NewGuid().ToString("N");

            CreateDatabase(DBName);

            var ConnectionString = (new SqlConnectionStringBuilder()
            {
                DataSource = "(localdb)\\MSSQLLocalDB",
                InitialCatalog = DBName,
                IntegratedSecurity = true,
                ApplicationName = "EntityFramework"
            }).ToString();
            return new EternitySqlStorage(ConnectionString, clock, TimeSpan.FromMilliseconds(100));
        }

        public override void Dispose()
        {
            if (DBName != null)
            {
                DeleteDatabase(DBName);
                try
                {
                    System.IO.File.Delete($"{filePath}.mdf");
                    System.IO.File.Delete($"{filePath}.ldf");
                }
                catch { }
                filePath = null;
            }
        }

        private void CreateDatabase(string DBName)
        {
            var name = System.IO.Path.Combine(System.IO.Path.GetTempPath(), DBName);

            var DbFile = $"{name}.mdf";
            var LogFile = $"{name}.ldf";

            this.filePath = name;

            Execute($"CREATE DATABASE [{DBName}] ON PRIMARY (NAME = {DBName}_data, FILENAME='{DbFile}') LOG ON (NAME={DBName}_Log, FILENAME='{LogFile}')");
        }

        private void Execute(string command)
        {
            using (var c = new SqlConnection($"server=(localdb)\\MSSQLLocalDB"))
            {
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
            Execute("USE master;");
            Execute($"ALTER DATABASE [{DBName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;");
            Execute($"DROP DATABASE [{DBName}]");
        }
    }
}
