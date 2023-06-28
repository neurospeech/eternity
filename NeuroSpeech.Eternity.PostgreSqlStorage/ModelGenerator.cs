using NeuroSpeech.TemplatedQuery;
using Npgsql;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace NeuroSpeech.Eternity
{
    internal class ModelCreator
    {
        internal static async Task CreateAsync(NpgsqlConnection conn, 
            Literal schemaName,
            Literal tableName)
        {
            // create workflows table..
            var createScript = TemplateQuery.New(@$"

    CREATE SCHEMA IF NOT EXISTS ""{schemaName}"";

    CREATE TABLE IF NOT EXISTS ""{schemaName}"".""[{tableName}"" (

        ""nID"" bigserial,
        ""ID"" text NOT NULL,
        ""IDHash"" character varying(400) NOT NULL,
        ""Name"" character varying(200) NOT NULL,
        ""Input"" text,
        ""IsWorkflow"" bit NOT NULL,
        ""UtcETA"" timestamp without time zone NOT NULL,
        ""UtcCreated"" timestamp without time zone NOT NULL,
        ""UtcUpdated"" timestamp without time zone NOT NULL,
        ""Response"" text,
        ""State"" character varying(20),
        ""ParentID"" text,
        ""ParentIDHash"" character varying(400),
        ""Priority"" integer NOT NULL,
        ""Extra"" text,
        ""QueueTTL"" timestamp without time zone,
        ""LockToken"" character varying(400),
        ""LockTTL"" timestamp without time zone,
        PRIMARY KEY (""nID"")

    );

        CREATE INDEX IF NOT EXISTS ""IX_{schemaName}_{tableName}_IDHash""
            ON ""{schemaName}"".""{tableName}"" USING btree (""IDHash"" ASC NULLS LAST) INCLUDE (""ID"");

        CREATE INDEX IF NOT EXISTS ""IX_{schemaName}_{tableName}_ParentIDHash""
            ON ""{schemaName}"".""{tableName}"" (
                ""ParentIDHash"" ASC NULLS LAST,
                ""Priority"" DESC NULLS FIRST
            )
            INCLUDE (""ParentID"") WHERE ""ParentIDHash"" IS NOT NULL;

        CREATE INDEX ""IX_{schemaName}_{tableName}_UtcETA""
            ON ""{schemaName}"".""{tableName}"" (
                ""UtcETA"" ASC NULLS LAST,
                ""Priority"" DESC NULLS FIRST
            ) INCLUDE(""QueueTTL"") WHERE ""IsWorkflow"" = 1;

");
            await conn.ExecuteNonQueryAsync(createScript);
        }
    }
}
