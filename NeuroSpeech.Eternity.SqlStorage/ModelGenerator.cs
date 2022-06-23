using Microsoft.Data.SqlClient;
using NeuroSpeech.TemplatedQuery;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace NeuroSpeech.Eternity
{
    internal class ModelCreator
    {
        internal static async Task CreateAsync(SqlConnection conn, 
            Literal schemaName,
            Literal tableName)
        {
            // create workflows table..
            var createScript = TemplateQuery.New(@$"
    IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = '{schemaName}')
    BEGIN
        EXEC('CREATE SCHEMA [{schemaName}]')
    END

    IF object_id('[{schemaName}].[{tableName}]') is null
    BEGIN
        CREATE TABLE [{schemaName}].[{tableName}] (
            [nID] BIGINT IDENTITY(1,1) NOT NULL,
            ID nvarchar(max),
            IDHash nvarchar(400),
            Name nvarchar(200) NOT NULL,
            Input nvarchar(max),
            IsWorkflow bit,
            UtcETA DATETIME2 NOT NULL,
            UtcCreated DATETIME2 NOT NULL,
            UtcUpdated DATETIME2 NOT NULL,
            Response nvarchar(max),
            State nvarchar(20),
            ParentID nvarchar(max),
            ParentIDHash nvarchar(400),
            Priority int,
            Extra nvarchar(max) NULL,
            QueueTTL DATETIME2 NULL,
            LockToken nvarchar(400) NULL,
            LockTTL DATETIME2 NULL,
        CONSTRAINT [{schemaName}_PK_{tableName}] PRIMARY KEY CLUSTERED (
            [nID] ASC
        ));

        CREATE INDEX [IX_{schemaName}_{tableName}_IDHash]
        ON [{schemaName}].[{tableName}] (IDHash) INCLUDE (ID);

        CREATE INDEX [IX_{schemaName}_{tableName}_ParentIDHash]
        ON [{schemaName}].[{tableName}] (ParentIDHash, Priority DESC) INCLUDE (ParentID) WHERE ParentIDHash IS NOT NULL;

        CREATE INDEX [IX_{schemaName}_{tableName}_UtcETA]
        ON [{schemaName}].[{tableName}] (UtcETA, Priority DESC) WHERE IsWorkflow = 1;

    END
");
            await conn.ExecuteNonQueryAsync(createScript);
        }
    }
}
