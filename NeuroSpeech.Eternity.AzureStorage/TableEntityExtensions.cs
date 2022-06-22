using Azure;
using Azure.Data.Tables;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Specialized;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace NeuroSpeech.Eternity
{
    public static class TableEntityExtensions
    {
        public static Task UploadTextAsync(this BlobClient client, string text)
        {
            return client.UploadAsync(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(text)));
        }

        public static async Task<string> DownloadTextAsync(this BlobClient client)
        {
            var r = await client.DownloadAsync();
            using (r.Value) {
                var sr = new StreamReader(r.Value.Content, System.Text.Encoding.UTF8);
                return await sr.ReadToEndAsync();
            }
        }


        //public static TableEntity ToTableEntity(this ActivityStep key, string rowKey)
        //{
        //    var te = new TableEntity(key.ID, rowKey)
        //        {
        //            { nameof(key.Method), key.Method },
        //            { nameof(key.ID), key.ID },
        //            { nameof(key.ActivityType), Enum.GetName(typeof(ActivityType), key.ActivityType) },
        //            { nameof(key.SequenceID), key.SequenceID },
        //            { nameof(key.DateCreated), key.DateCreated },
        //            { nameof(key.LastUpdated), key.LastUpdated },
        //            { nameof(key.ETA), key.ETA },
        //            { nameof(key.Status) , Enum.GetName(typeof(ActivityStatus), key.Status) }
        //        };
        //    if(key.QueueToken != null)
        //    {
        //        te.Add(nameof(key.QueueToken), key.QueueToken);
        //    }
        //    if(key.Result != null)
        //    {
        //        te.Add(nameof(key.Result), key.Result);
        //    }
        //    if(key.Error != null)
        //    {
        //        te.Add(nameof(key.Error), key.Error);
        //    }
        //    if(key.Key != null)
        //    {
        //        te.Add(nameof(key.Key), key.Key);
        //    }
        //    return te;
        //}

        public static async Task DeleteAllAsync(this TableClient client, IEnumerable<(string partitionKey, string rowKey)> items)
        {
            while (items.Any())
            {
                var top = items.Take(100).Select(x => new TableTransactionAction(TableTransactionActionType.Delete,
                    new TableEntity(x.partitionKey, x.rowKey), ETag.All
                    )).ToList();
                foreach (var item in top)
                {
                    try
                    {
                        await client.DeleteEntityAsync(item.Entity.PartitionKey, item.Entity.RowKey, ETag.All);
                    }
                    catch (RequestFailedException ex)
                    {
                        if (ex.Status == 419 || ex.Status == 404)
                            continue;
                        throw;
                    }
                }
                items = items.Skip(100);
            }
        }
        public static async Task DeleteAllAsync(this TableClient client, string partitionKey)
        {
            var filter = Azure.Data.Tables.TableClient.CreateQueryFilter($"PartitionKey eq {partitionKey}");
            List<TableTransactionAction> actions = new List<TableTransactionAction>();
            while (true)
            {
                await foreach (var step in client.QueryAsync<TableEntity>(filter, select: new string[] { }, maxPerPage: 100))
                {
                    actions.Add(new TableTransactionAction(TableTransactionActionType.Delete,
                        new TableEntity(step.PartitionKey, step.RowKey),
                        ETag.All));
                    if (actions.Count == 100)
                        break;
                }
                if (actions.Count == 0)
                    break;
                await client.SubmitTransactionAsync(actions);
                actions.Clear();
            }
        }

        public static TableEntity ToTableEntity<T>(this T item, string partitionKey, string rowKey)
            where T : class, new()
        {
            Type type = typeof(T);
            var entity = new TableEntity(partitionKey, rowKey);
            foreach (var property in type.GetProperties())
            {
                if (property.CanRead && property.CanWrite)
                {
                    Type propertyType = property.PropertyType;
                    if (propertyType.IsEnum)
                    {
                        entity.Add(property.Name, propertyType.GetEnumName(property.GetValue(item)));
                        continue;
                    }
                    entity.Add(property.Name, property.GetValue(item));
                }
            }
            return entity;
        }

        public static T ToObject<T>(this TableEntity entity)
        {
            Type type = typeof(T);
            var result = Activator.CreateInstance<T>();
            foreach (var property in type.GetProperties())
            {
                if (property.CanRead && property.CanWrite)
                {
                    if (entity.TryGetValue(property.Name, out var text))
                    {
                        Type propertyType = property.PropertyType;
                        if (propertyType.IsEnum)
                        {
                            property.SetValue(result, Enum.Parse(propertyType, text.ToString()));
                            continue;
                        }
                        property.SetValue(result, text);
                    }
                }
            }
            return result;
        }
    }
}
