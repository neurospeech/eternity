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
        internal static async Task CreateAsync(
            MySqlConnection conn,
            Literal tableName)
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
        UtcETA DATETIME NOT NULL,
        UtcCreated DATETIME NOT NULL,
        UtcUpdated DATETIME NOT NULL,
        Response TEXT,
        State VARCHAR(20),
        ParentID TEXT,
        ParentIDHash VARCHAR(400),
        Priority INTEGER,
        Extra TEXT,
        QueueTTL DATETIME,
        LockToken VARCHAR(400),
        LockTTL DATETIME,
        PRIMARY KEY (nID)
    );

    CREATE INDEX IX_{tableName}_ID
    ON {tableName} (IDHash, Priority);

    CREATE INDEX IX_{tableName}_ParentID
    ON {tableName} (ParentIDHash, Priority DESC);

    CREATE INDEX IX_{tableName}_UtcETA
    ON {tableName} (IsWorkflow, UtcETA, Priority DESC);

    CREATE PROCEDURE Save{tableName} (
        IN pID TEXT,
        IN pIDHash VARCHAR(400),
        IN pName VARCHAR(200),
        IN pInput TEXT,
        IN pIsWorkflow TINYINT(1),
        IN pUtcETA DATETIME,
        IN pUtcCreated DATETIME,
        IN pUtcUpdated DATETIME,
        IN pResponse TEXT,
        IN pState VARCHAR(20),
        IN pParentID TEXT,
        IN pParentIDHash VARCHAR(400),
        IN pPriority INTEGER,
        IN pExtra TEXT,
        IN pQueueTTL DATETIME,
        IN pLockToken VARCHAR(400),
        IN pLockTTL DATETIME
    )
    BEGIN
        IF EXISTS (SELECT 1 FROM {tableName} WHERE IDHash=pIDHash AND ID=pID) THEN
            UPDATE {tableName} SET
                ID = pID,
                IDHash = pIDHash,
                Name = pName,
                Input = pInput,
                IsWorkflow = pIsWorkflow,
                QueueTTL = CASE WHEN pUtcETA <> Target.UtcETA THEN NULL ELSE QueueTTL END,
                UtcETA = pUtcETA,
                UtcCreated = pUtcCreated,
                UtcUpdated = pUtcUpdated,
                Response = pResponse,
                State = pState,
                ParentID = pParentID,
                ParentIDHash = pParentIDHash,
                Priority = pPriority
            WHERE
            IDHash=pIDHash AND ID=pID
        ELSE
            INSERT INTO {tableName} (
                ID, IDHash, Name, Input, IsWorkflow, UtcETA, UtcCreated, UtcUpdated,
                Response, State, ParentID, ParentIDHash, Priority
            ) VALUES (
                pID, pIDHash,pName,pInput,pIsWorkflow,
                pUtcTicks, pUtcTicks, pUtcTicks,
                pResponse, pState, pParentID, pParentIDHash,
                pPriority
            )
        END IF
    END
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
