using MySql.Data.MySqlClient;
using NeuroSpeech.TemplatedQuery;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace NeuroSpeech.Eternity
{
    internal class ModelCreator
    {
        internal static async Task CreateAsync(MySqlConnection conn, string tableName)
        {
            // create workflows table..
            var createScript = TemplateQuery.New(@$"
    CREATE TABLE IF NOT EXISTS {tableName} (
        nID BIGINT NOT NULL AUTO_INCREMENT,
        ID TEXT,
        IDHash VARCHAR(400),
        Name VARCHAR(200) NOT NULL,
        Input TEXT,
        IsWorkflow TINYINT(1),
        UtcETA INTEGER NOT NULL,
        UtcCreated INTEGER NOT NULL,
        UtcUpdated INTEGER NOT NULL,
        Response TEXT,
        State TEXT,
        ParentID TEXT,
        ParentIDHash VARCHAR(400),
        Priority INTEGER,
        PRIMARY KEY (nID)
    );

    CREATE INDEX IX_Workflows_ID
    ON {tableName} (IDHash, Priority);

    CREATE INDEX IX_Workflows_ParentID
    ON {tableName} (ParentIDHash, Priority);

    CREATE INDEX IX_Workflows_UtcETA
    ON {tableName} (UtcETA,IsWorkflow);

    CREATE TABLE IF NOT EXISTS ActivityLocks (
        nID BIGINT PRIMARY KEY,
        ETA INTEGER
    );

    DELETE FROM ActivityLocks;

");
            await conn.ExecuteNonQueryAsync(createScript);

            // await conn.ExecuteNonQueryAsync(TemplateQuery.New($"PRAGMA journal_mode = 'wal'"));
        }
    }

    public class ActivityLock
    {
        public string ID { get; set; }
        public DateTime ETA { get; set; }

    }

}
