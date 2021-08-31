using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;

namespace NeuroSpeech.Eternity
{
    public enum ActivityType
    {
        Workflow,
        Activity,
        Delay,
        Event,
        Child
    }

    public class WorkflowStep
    {

        public string? WorkflowType { get; set; }

        public string? Category { get; set; }

        public string? Description { get; set; }

        /// <summary>
        /// ID of Parent Workflow
        /// </summary>
        public string? ParentID { get; set; }

        public string? ID { get; set; }
        public string? Parameter { get; set; }
        public DateTimeOffset ETA { get; set; }
        public DateTimeOffset DateCreated { get; set; }
        public DateTimeOffset LastUpdated { get; set; }

        public ActivityStatus Status { get; set; }

        public string? Result { get; set; }

        public string? Error { get; set; }

        public static WorkflowStep Workflow(
            string id,
            Type workflowType,
            object input,
            string? description,
            DateTimeOffset eta,
            DateTimeOffset now,
            string? parentID,
            JsonSerializerOptions? options = default)
        {
            var step = new WorkflowStep
            {
                WorkflowType = workflowType.AssemblyQualifiedName,
                Category = workflowType.Name,
                Description = description,
                ID = id,
                ParentID = parentID,
                Parameter = JsonSerializer.Serialize(input, options),
                // step.ParametersHash = Convert.ToBase64String(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(step.Parameters)));
                ETA = eta,
                DateCreated = now,
                LastUpdated = now
            };
            return step;
        }

        internal T? AsResult<T>(JsonSerializerOptions options)
        {
            if (Result == null)
                return default;
            return JsonSerializer.Deserialize<T>(Result!, options);
        }
    }

    public class ActivityStep
    {
        public static SHA256 sha = SHA256.Create();

        public string? Method { get; set; }

        public string? ID { get; set; }

        public ActivityType ActivityType { get; set; }

        public long SequenceID { get; set; }

        public DateTimeOffset DateCreated { get; set; }

        public DateTimeOffset LastUpdated { get; set; }

        public DateTimeOffset ETA { get; set; }

        // public string ParametersHash { get; set; }

        public string? Key { get; set; }

        public string KeyHash => Uri.EscapeDataString(Convert.ToBase64String(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(Key))));

        public string[]? GetEvents() => Parameters;

        private string[]? parameters;
        internal string[]? Parameters { get {
                if (parameters == null)
                {
                    if (Key == null)
                        return null;
                    var p = JsonSerializer.Deserialize<List<string?>>(Key, new JsonSerializerOptions { 
                    })!;
                    int n = p.Count - 4;
                    var pa = new string[n];
                    for (int i = 0; i < n; i++)
                    {
                        pa[i] = p[i+4]!;
                    }
                    parameters = pa;
                }
                return parameters;
            }
            set => parameters = value; 
        }

        public ActivityStatus Status { get; set; }

        public string? Error { get; set; }

        public string? Result { get; set; }

        public string? QueueToken { get; set; }

        internal T? AsResult<T>(JsonSerializerOptions options)
        {
            if (Result == null)
                return default;
            return JsonSerializer.Deserialize<T>(Result!, options);
        }

        public static ActivityStep Delay(
            string id,
            DateTimeOffset eta,
            DateTimeOffset now)
        {
            var step = new ActivityStep
            {
                ActivityType = ActivityType.Delay,
                ID = id,
                Parameters = new string[] { },
                ETA = eta,
                DateCreated = now,
                LastUpdated = now
            };
            step.SetKey(now.UtcTicks);
            return step;
        }

        public static ActivityStep Event(
            string id,
            string[] events,
            DateTimeOffset eta,
            DateTimeOffset now)
        {
            var step = new ActivityStep
            {
                ActivityType = ActivityType.Event,
                ID = id,
                Parameters = events,
                // step.ParametersHash = Convert.ToBase64String(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(step.Parameters)));
                ETA = eta,
                DateCreated = now,
                LastUpdated = now
            };
            step.SetKey(now.UtcTicks);
            return step;
        }


        public static ActivityStep Activity(
            bool uniqueParameters,
            string id,
            MethodInfo method,
            object[] parameters,
            DateTimeOffset eta,
            DateTimeOffset now,
            JsonSerializerOptions? options = default)
        {
            var step = new ActivityStep
            {
                ActivityType = ActivityType.Activity,
                ID = id,
                Method = method.Name,
                Parameters = parameters.Select(x => JsonSerializer.Serialize(x, options)).ToArray(),
                ETA = eta,
                DateCreated = now,
                LastUpdated = now
            };
            step.SetKey(uniqueParameters ? 0 : now.UtcTicks);
            // step.ParametersHash = Convert.ToBase64String(sha.ComputeHash( System.Text.Encoding.UTF8.GetBytes(step.Parameters)));
            return step;
        }

        internal static ActivityStep Child(
            string parentID, 
            Type type, 
            object parameters , 
            DateTimeOffset eta, 
            DateTimeOffset utcNow,
            JsonSerializerOptions? options = null)
        {
            var step = new ActivityStep
            {
                ID = parentID,
                ActivityType = ActivityType.Child,
                Parameters = new string[] { JsonSerializer.Serialize(parameters, options) },
                ETA = eta,
                DateCreated = utcNow,
                LastUpdated = utcNow
            };
            step.SetKey(0);
            return step;
        }

        private void SetKey(long ticks) {
            var list = new List<string?>(4 + Parameters!.Length) {
                this.ID,
                this.ActivityType.ToString(),
                this.Method,
                ticks.ToString(),
            };
            list.AddRange(Parameters);
            this.Key = JsonSerializer.Serialize(list);
            Parameters = null;
        }
    }
}
