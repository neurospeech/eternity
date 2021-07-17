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
    CREATE TABLE IF NOT EXISTS Workflows (
        ID TEXT PRIMARY KEY,
        WorkflowType TEXT NOT NULL,
        Category TEXT,
        Parameter TEXT,
        Description TEXT,
        ETA INTEGER NOT NULL,
        DateCreated INTEGER NOT NULL,
        LastUpdated INTEGER NOT NULL,
        ParentID TEXT,
        Status TEXT,
        Result TEXT,
        Error Text
    );

    CREATE INDEX IF NOT EXISTS IX_Workflows_DateCreated
    ON Workflows (DateCreated);

    CREATE INDEX IF NOT EXISTS IX_Workflows_ParentID
    ON Workflows (ParentID) WHERE ParentID IS NOT NULL;


    CREATE TABLE IF NOT EXISTS Activities (
        SequenceID INTEGER PRIMARY KEY AUTOINCREMENT,
        ID TEXT NOT NULL,
        ActivityType TEXT,
        Method TEXT,
        Category TEXT,
        Parameters TEXT,
        ETA INTEGER NOT NULL,
        DateCreated INTEGER NOT NULL,
        LastUpdated INTEGER NOT NULL,
        Status TEXT,
        Result TEXT,
        Error Text,
        Key TEXT NOT NULL,
        KeyHash TEXT NOT NULL,
        QueueToken TEXT
    );
    
    CREATE INDEX IF NOT EXISTS IX_Activities_KeyHash
    ON Activities (ID, KeyHash);

    CREATE TABLE IF NOT EXISTS ActivityEvents (
        ROWID INTEGER PRIMARY KEY AUTOINCREMENT,
        ID TEXT,
        EventName TEXT,
        SequenceID INTEGER
    );

    CREATE INDEX IF NOT EXISTS IX_ActivityEvents_Index
    ON ActivityEvents (ID, EventName);

    CREATE TABLE IF NOT EXISTS QueueTokens (
        Token INTEGER PRIMARY KEY AUTOINCREMENT,
        ID TEXT,
        ETA INTEGER,
        ETALocked INTEGER,
        CID INTEGER,
        Command TEXT);

    CREATE INDEX IF NOT EXISTS IX_QueueTokens_ETA
    ON QueueTokens (ETA);

    CREATE TABLE IF NOT EXISTS ActivityLocks (
        SequenceID INTEGER PRIMARY KEY
    );

");
            await conn.ExecuteNonQueryAsync(createScript);
        }
    }

    public class QueueToken
    {
        public long Token { get; set; }
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        public string ID { get; set; }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

        public long ETA { get; set; }

        public long ETALocked { get; set; }

        /// <summary>
        /// Concurrency Token
        /// </summary>
        public long CID { get; set; }
    }

    public class ActivityLock: IEternityLock
    {
        public long SequenceID { get; set; }
    }
}
