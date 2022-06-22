using NeuroSpeech.TemplatedQuery;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace NeuroSpeech.Eternity
{
    public static class DbConnectionHelper
    {
        public class DbReader : IDisposable
        {
            private DbConnection conn;
            private DbCommand cmd;
            private DbDataReader? reader;

            public DbReader(DbConnection conn, DbCommand cmd)
            {
                this.conn = conn;
                this.cmd = cmd;
            }

            public DbDataReader? Reader => reader;

            public DbCommand Command => cmd;

            public void Dispose()
            {
                try
                {
                    reader?.Dispose();
                }
                catch { }
                try
                {
                    cmd?.Dispose();
                }
                catch { }

                // Since EF will destroy connection, we should not
                //try
                //{
                //    conn?.Dispose();
                //}
                //catch { }
            }

            public static DbReader Create(DbConnection db, TemplateQuery query)
            {
                var r = CreateCommand(db, query);
                var cmd = r.cmd;
                r.reader = cmd.ExecuteReader(CommandBehavior.SequentialAccess);
                return r;
            }

            public static DbReader CreateCommand(DbConnection db, TemplateQuery query)
            {
                if (db.State != ConnectionState.Open)
                {
                    db.Open();
                }
                DbReader r = new DbReader(db, db.CreateCommand());
                var cmd = r.cmd;
                string text = query.Text;
                //if (db.Database.IsMySql())
                //{
                //    text = text
                //        .Replace("[dbo].", "")
                //        .Replace("[", "`")
                //        .Replace("]", "`");
                //}
                cmd.CommandText = text;
                foreach (var kvp in query.Values)
                {
                    var p = cmd.CreateParameter();
                    p.ParameterName = kvp.Key;
                    var value = kvp.Value ?? DBNull.Value;
                    if (value.GetType().IsEnum)
                    {
                        p.Value = value.ToString();
                    }
                    else
                    {
                        if (value is DateTimeOffset d)
                        {
                            p.Value = d.UtcDateTime;
                        }
                        else
                        {
                            p.Value = value;
                        }
                    }
                    cmd.Parameters.Add(p);
                }
                return r;
            }

            public static async Task<DbReader> CreateAsync(DbConnection db, TemplateQuery query)
            {
                var r = await CreateCommandAsync(db, query);
                var cmd = r.cmd;
                r.reader = await cmd.ExecuteReaderAsync(CommandBehavior.SequentialAccess);
                return r;
            }

            public static async Task<DbReader> CreateCommandAsync(DbConnection db, TemplateQuery query)
            {
                if (db.State != ConnectionState.Open)
                {
                    await db.OpenAsync();
                }
                DbReader r = new DbReader(db, db.CreateCommand());
                var cmd = r.cmd;
                string text = query.Text;
                //if (db.Database.IsMySql())
                //{
                //    text = text
                //        .Replace("[dbo].", "")
                //        .Replace("[", "`")
                //        .Replace("]", "`");
                //}
                cmd.CommandText = text;
                foreach (var kvp in query.Values)
                {
                    var p = cmd.CreateParameter();
                    p.ParameterName = kvp.Key;
                    var value = kvp.Value ?? DBNull.Value;
                    if (value.GetType().IsEnum)
                    {
                        p.Value = value.ToString();
                    }
                    else
                    {
                        if (value is DateTimeOffset d)
                        {
                            p.Value = d.UtcDateTime;
                        }
                        else
                        {
                            p.Value = value;
                        }
                    }
                    cmd.Parameters.Add(p);
                }
                return r;
            }

        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="db"></param>
        /// <param name="query"></param>
        /// <returns></returns>
        public static JArray FromSqlToJson(
            this DbConnection db,
            TemplateQuery query)
        {
            JArray list = new JArray();
            var props = new List<(int index, string name)>();
            using (var dbReader = DbReader.Create(db, query))
            {
                var reader = dbReader.Reader;
                while (reader.Read())
                {
                    if (props.Count == 0)
                    {
                        if (reader.FieldCount == 0)
                        {
                            return list;
                        }
                        for (int i = 0; i < reader.FieldCount; i++)
                        {
                            var n = reader.GetName(i);
                            props.Add((i, n));
                        }

                    }

                    var item = new JObject();

                    foreach (var (index, n) in props)
                    {
                        var value = reader.GetValue(index);
                        if (value == null || value == DBNull.Value)
                        {
                            continue;
                        }
                        item.Add(n, JToken.FromObject(value));
                    }
                    list.Add(item);
                }
                return list;
            }
        }

        ///// <summary>
        ///// 
        ///// </summary>
        ///// <param name="db"></param>
        ///// <param name="format"></param>
        ///// <param name="parameters"></param>
        ///// <returns></returns>
        //public static Task<JArray> FromSqlToJsonAsync(
        //    this DbContext db,
        //    string format,
        //    params object[] parameters)
        //{
        //    return FromSqlToJsonAsync(db, TemplateQuery.FromString(format, parameters));
        //}


        /// <summary>
        /// 
        /// </summary>
        /// <param name="db"></param>
        /// <param name="query"></param>
        /// <returns></returns>
        public static async Task<JArray> FromSqlToJsonAsync(
            this DbConnection db,
            TemplateQuery query)
        {
            JArray list = new JArray();
            var props = new List<(int index, string name)>();
            using (var dbReader = await DbReader.CreateAsync(db, query))
            {
                var reader = dbReader.Reader;
                while (await reader.ReadAsync())
                {
                    if (props.Count == 0)
                    {
                        if (reader.FieldCount == 0)
                        {
                            return list;
                        }
                        for (int i = 0; i < reader.FieldCount; i++)
                        {
                            var n = reader.GetName(i);
                            props.Add((i, n));
                        }

                    }

                    var item = new JObject();

                    foreach (var (index, n) in props)
                    {
                        var value = reader.GetValue(index);
                        if (value == null || value == DBNull.Value)
                        {
                            continue;
                        }
                        item.Add(n, JToken.FromObject(value));
                    }
                    list.Add(item);
                }
                return list;
            }
        }

        public static async Task<int> ExecuteNonQueryAsync(this DbConnection db, TemplateQuery query)
        {
            using (var r = await DbReader.CreateCommandAsync(db, query))
            {
                return await r.Command.ExecuteNonQueryAsync();
            }
        }

        public static int ExecuteNonQuery(this DbConnection db, TemplateQuery query)
        {
            using (var r = DbReader.CreateCommand(db, query))
            {
                return r.Command.ExecuteNonQuery();
            }
        }

        public static async Task<object> ExecuteScalarAsync(this DbConnection db, TemplateQuery query)
        {
            using (var r = await DbReader.CreateCommandAsync(db, query))
            {
                return await r.Command.ExecuteScalarAsync();
            }
        }

        public static object ExecuteScalar(this DbConnection db, TemplateQuery query)
        {
            using (var r = DbReader.CreateCommand(db, query))
            {
                return r.Command.ExecuteScalar();
            }
        }

        private static ConcurrentDictionary<string, PropertyInfo> propertyCache
            = new ConcurrentDictionary<string, PropertyInfo>();

        public static List<T> FromSql<T>(
            this DbConnection db,
            TemplateQuery[] queries,
            bool ignoreUnmatchedProperties = false)
            where T : class
        {
            if (queries == null || queries.Length == 0)
                throw new ArgumentException($"No query specified");
            TemplateQuery q = (FormattableString)$"";
            for (int i = 0; i < queries.Length; i++)
            {
                q = queries[i];
                if (i == queries.Length - 1)
                {
                    break;
                }
                db.ExecuteNonQuery(q);
            }
            return db.FromSql<T>(q, ignoreUnmatchedProperties);
        }

        public static async Task<List<T>> FromSqlAsync<T>(
            this DbConnection db,
            TemplateQuery[] queries,
            bool ignoreUnmatchedProperties = false)
            where T : class
        {
            if (queries == null || queries.Length == 0)
                throw new ArgumentException($"No query specified");
            TemplateQuery q = (FormattableString)$"";
            for (int i = 0; i < queries.Length; i++)
            {
                q = queries[i];
                if (i == queries.Length - 1)
                {
                    break;
                }
                await db.ExecuteNonQueryAsync(q);
            }
            return await db.FromSqlAsync<T>(q, ignoreUnmatchedProperties);
        }

        public static async Task<T> FirstOrDefaultAsync<T>(this DbConnection conn, TemplateQuery query, bool ignoreUnmatchedProperties = false)
            where T : class
        {
            var list = await FromSqlAsync<T>(conn, query, ignoreUnmatchedProperties);
            return list.FirstOrDefault();
        }

        public static async Task<List<T>> FromSqlAsync<T>(
            this DbConnection db,
            TemplateQuery query,
            bool ignoreUnmatchedProperties = false)
            where T : class
        {
            List<T> list = new List<T>();
            var props = new List<(int index, PropertyInfo property, string name)>();
            using (var dbReader = await DbReader.CreateAsync(db, query))
            {
                var reader = dbReader.Reader;
                while (await reader.ReadAsync())
                {
                    if (props.Count == 0)
                    {
                        if (reader.FieldCount == 0)
                        {
                            return list;
                        }
                        Type type = typeof(T);
                        List<string> notFound = new List<string>();
                        for (int i = 0; i < reader.FieldCount; i++)
                        {
                            var n = reader.GetName(i);
                            var key = $"{type.FullName}.{n}";
                            var p = propertyCache.GetOrAdd(key, a => type.GetProperty(n, BindingFlags.IgnoreCase | BindingFlags.Instance | BindingFlags.SetProperty | BindingFlags.Public));
                            props.Add((i, p, n));
                        }

                        var empty = props.Where(x => x.property == null).Select(x => x.name);
                        if (empty.Any())
                        {
                            if (!ignoreUnmatchedProperties)
                            {
                                throw new InvalidOperationException($"Properties {string.Join(",", empty)} not found in {type.FullName}");
                            }
                            props = props.Where(x => x.property != null).ToList();
                        }

                    }

                    var item = Activator.CreateInstance<T>();

                    foreach (var (index, property, n) in props)
                    {
                        var value = reader.GetValue(index);
                        if (value == null || value == DBNull.Value)
                        {
                            continue;
                        }
                        property.ConvertAndSetValue(item, value);

                    }
                    list.Add(item);
                }
                return list;
            }
        }

        public static List<T> FromSql<T>(
    this DbConnection db,
    TemplateQuery query,
    bool ignoreUnmatchedProperties = false)
    where T : class
        {
            List<T> list = new List<T>();
            var props = new List<(int index, PropertyInfo property, string name)>();
            using (var dbReader = DbReader.Create(db, query))
            {
                var reader = dbReader.Reader;
                while (reader.Read())
                {
                    if (props.Count == 0)
                    {
                        if (reader.FieldCount == 0)
                        {
                            return list;
                        }
                        Type type = typeof(T);
                        List<string> notFound = new List<string>();
                        for (int i = 0; i < reader.FieldCount; i++)
                        {
                            var n = reader.GetName(i);
                            var key = $"{type.FullName}.{n}";
                            var p = propertyCache.GetOrAdd(key, a => type.GetProperty(n, BindingFlags.IgnoreCase | BindingFlags.Instance | BindingFlags.SetProperty | BindingFlags.Public));
                            props.Add((i, p, n));
                        }

                        var empty = props.Where(x => x.property == null).Select(x => x.name);
                        if (empty.Any())
                        {
                            if (!ignoreUnmatchedProperties)
                            {
                                throw new InvalidOperationException($"Properties {string.Join(",", empty)} not found in {type.FullName}");
                            }
                            props = props.Where(x => x.property != null).ToList();
                        }

                    }

                    var item = Activator.CreateInstance<T>();

                    foreach (var (index, property, n) in props)
                    {
                        var value = reader.GetValue(index);
                        if (value == null || value == DBNull.Value)
                        {
                            continue;
                        }
                        property.ConvertAndSetValue(item, value);

                    }
                    list.Add(item);
                }
                return list;
            }
        }



        public static void ConvertAndSetValue(this PropertyInfo property, object target, object? value)
        {
            if (value == null || !property.CanWrite)
                return;
            var valueType = value.GetType();
            var tc = Type.GetTypeCode(valueType);
            var propertyType = property.PropertyType;
            propertyType = Nullable.GetUnderlyingType(propertyType) ?? propertyType;
            if (propertyType == valueType)
            {
                property.SetValue(target, value);
                return;
            }
            if (propertyType.IsEnum)
            {
                switch (tc)
                {
                    case TypeCode.String:
                        property.SetValue(target, Enum.Parse(propertyType, (string)value, true));
                        return;
                    case TypeCode.UInt64:
                        property.SetValue(target, Enum.ToObject(propertyType, (ulong)value));
                        break;
                    case TypeCode.UInt32:
                        property.SetValue(target, Enum.ToObject(propertyType, (uint)value));
                        break;
                    case TypeCode.UInt16:
                        property.SetValue(target, Enum.ToObject(propertyType, (ushort)value));
                        break;
                    case TypeCode.Int64:
                        property.SetValue(target, Enum.ToObject(propertyType, (long)value));
                        break;
                    case TypeCode.Int32:
                        property.SetValue(target, Enum.ToObject(propertyType, (int)value));
                        break;
                    case TypeCode.Int16:
                        property.SetValue(target, Enum.ToObject(propertyType, (short)value));
                        break;
                    case TypeCode.Byte:
                        property.SetValue(target, Enum.ToObject(propertyType, (byte)value));
                        break;
                    case TypeCode.SByte:
                        property.SetValue(target, Enum.ToObject(propertyType, (sbyte)value));
                        break;
                }
                throw new InvalidOperationException();
            }
            if (propertyType == typeof(DateTimeOffset))
            {
                switch (tc)
                {
                    case TypeCode.Int64:
                        property.SetValue(target, new DateTimeOffset((long)value, TimeSpan.Zero));
                        return;
                    case TypeCode.Int32:
                        property.SetValue(target, new DateTimeOffset((long)(int)value, TimeSpan.Zero));
                        return;
                    case TypeCode.DateTime:
                        property.SetValue(target, new DateTimeOffset(((DateTime)value).Ticks, TimeSpan.Zero));
                        return;
                }
            }
            if (propertyType == typeof(DateTime))
            {
                switch (tc)
                {
                    case TypeCode.Int64:
                        property.SetValue(target, new DateTime((long)value, DateTimeKind.Utc));
                        return;
                    case TypeCode.Int32:
                        property.SetValue(target, new DateTime((long)(int)value, DateTimeKind.Utc));
                        return;
                }
            }
            property.SetValue(target, Convert.ChangeType(value, propertyType));
        }
    }
}
