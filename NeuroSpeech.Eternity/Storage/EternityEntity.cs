﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

namespace NeuroSpeech.Eternity.Storage
{
    public class EternityEntity
    {
        public EternityEntity()
        {

        }

        internal EternityEntity(string id, string name, string? input)
        {
            ID = id;
            Name = name;
            Input = input;
            IsWorkflow = true;
        }

        internal static EternityEntity From(
            string id,
            string name,
            bool uniqueParameters,
            object?[] parameters,
            DateTimeOffset eta,
            DateTimeOffset now,
            JsonSerializerOptions options)
        {
            var @this = new EternityEntity(id, name, null);
            @this.IsWorkflow = false;
            @this.Name = name;
            @this.UtcETA = eta.UtcDateTime;
            @this.UtcUpdated = @this.UtcCreated = now.UtcDateTime;
            @this.ParentID = id;
            var dt = uniqueParameters ? "" : $"-{now.ToSqlServerPrecision().Ticks}";

            var inputJson = new string[parameters.Length];
            var uk = new StringBuilder();
            for (int i = 0; i < parameters.Length; i++)
            {
                var json = JsonSerializer.Serialize(parameters[i], options);
                inputJson[i] = json;
                uk.Append(json);
                uk.Append(',');
            }
            @this.ID = $"{id}{dt}-{name}-{uk}";
            return @this;
        }

        /// <summary>
        /// Entity Identifier, Must be unique
        /// </summary>
        public string ID { get; set; }

        /// <summary>
        /// ClrType of the Root Workflow
        /// or
        /// Clr Method Name of the Workflow Activity
        /// </summary>
        public string Name { get; set; }

        public string? Input { get; set; }

        public bool IsWorkflow { get; set; }

        public DateTimeOffset UtcETA { get; set; }

        
        public DateTimeOffset UtcCreated { get; set; }

        public DateTimeOffset UtcUpdated { get; set; }

        public string? Response { get; set; }

        public EternityEntityState State { get; set; }

        public string? ParentID { get; set; }

        public int Priority { get; set; }

        public string Extra {
            get { 
                if (ExtraDictionary.Count == 0)
                {
                    return "{}";
                }
                return JsonSerializer.Serialize(ExtraDictionary);
            }
            set {
                try
                {
                    var json = JsonDocument.Parse(value);
                    foreach(var item in json.RootElement.EnumerateObject())
                    {
                        var key = item.Name;
                        var v = item.Value;
                        switch (v.ValueKind)
                        {
                            case JsonValueKind.String:
                                if (v.GetString() is String sv)
                                {
                                    ExtraDictionary[key] = sv;
                                }
                                break;
                        }
                    }

                }
                catch {
                    ExtraDictionary.Clear();
                }
            }
        }

        internal readonly IDictionary<string, string> ExtraDictionary = new Dictionary<string, string>();
    }
}
