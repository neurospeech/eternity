using Microsoft.Data.Sqlite;
using NeuroSpeech.TemplatedQuery;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace NeuroSpeech.Eternity
{
    internal class ModelCreator
    {
        internal static async Task CreateAsync(SqliteConnection conn)
        {
            // create workflows table..
            var createScript = TemplateQuery.New(@$"
    CREATE TABLE IF NOT EXISTS EternityEntities (
        ID TEXT PRIMARY KEY,
        Name TEXT NOT NULL,
        Input TEXT,
        IsWorkflow BOOLEAN,
        UtcETA INTEGER NOT NULL,
        UtcCreated INTEGER NOT NULL,
        UtcUpdated INTEGER NOT NULL,
        Response TEXT,
        State TEXT,
        ParentID TEXT,
        Priority INTEGER,
        CurrentWaitingID TEXT
    );

    CREATE INDEX IF NOT EXISTS IX_Workflows_ParentID
    ON EternityEntities (ParentID) WHERE ParentID IS NOT NULL;

    CREATE INDEX IF NOT EXISTS IX_Workflows_UtcETA
    ON EternityEntities (UtcETA) WHERE IsWorkflow = 1;

    CREATE TABLE IF NOT EXISTS ActivityLocks (
        ID TEXT PRIMARY KEY,
        ETA INTEGER
    );

    DELETE FROM ActivityLocks;

");
            await conn.ExecuteNonQueryAsync(createScript);

            await conn.ExecuteNonQueryAsync(TemplateQuery.New($"PRAGMA journal_mode = 'wal'"));
        }
    }

    public class ActivityLock
    {
        public string ID { get; set; }
        public DateTime ETA { get; set; }

    }

}
