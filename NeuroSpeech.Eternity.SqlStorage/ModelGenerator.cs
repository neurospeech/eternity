﻿using Microsoft.Data.SqlClient;
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
    IF object_id('[{schemaName}]') is null
    BEGIN
        EXEC ('CREATE SCHEMA {schemaName};')
    END;
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
        CONSTRAINT [PK_History] PRIMARY KEY CLUSTERED (
            [nID] ASC
        ));

        CREATE INDEX [{schemaName}].[IX_{tableName}_IDHash]
        ON [{schemaName}].[{tableName}] (IDHash) INCLUDE (ID);

        CREATE INDEX [{schemaName}].[IX_{tableName}_ParentIDHash]
        ON [{schemaName}].[{tableName}] (ParentIDHash, Priority DESC) INCLUDE (ParentID) WHERE ParentIDHash IS NOT NULL;

        CREATE INDEX [{schemaName}].[IX_{tableName}_UtcETA]
        ON [{schemaName}].[{tableName}] (UtcETA, Priority DESC) WHERE IsWorkflow = 1;

    END
");
            await conn.ExecuteNonQueryAsync(createScript);
        }
    }
}
