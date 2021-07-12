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
            DateTimeOffset eta,
            DateTimeOffset now,
            JsonSerializerOptions? options = default)
        {
            var step = new WorkflowStep
            {
                WorkflowType = workflowType.AssemblyQualifiedName,
                Category = workflowType.Name,
                ID = id,
                Parameter = JsonSerializer.Serialize(input, options),
                // step.ParametersHash = Convert.ToBase64String(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(step.Parameters)));
                ETA = eta,
                DateCreated = now,
                LastUpdated = now
            };
            return step;
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

        public string KeyHash => Convert.ToBase64String(sha.ComputeHash( System.Text.Encoding.UTF8.GetBytes(Key)));

        public string? Parameters { get; set; }

        public ActivityStatus Status { get; set; }

        public string? Error { get; set; }

        public string? Result { get; set; }

        public string? QueueToken { get; set; }

        internal T? AsResult<T>(JsonSerializerOptions options)
        {
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
                Parameters = JsonSerializer.Serialize(eta.Ticks),
                ETA = eta,
                DateCreated = now,
                LastUpdated = now
            };
            step.Key = $"{step.ID}-{step.ActivityType}-{step.DateCreated.Ticks}-{step.Parameters}";
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
                Parameters = JsonSerializer.Serialize(events),
                // step.ParametersHash = Convert.ToBase64String(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(step.Parameters)));
                ETA = eta,
                DateCreated = now,
                LastUpdated = now
            };
            step.Key = $"{step.ID}-{step.ActivityType}-{step.DateCreated.Ticks}-{step.Parameters}";
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
                Parameters = JsonSerializer.Serialize(parameters.Select(x => JsonSerializer.Serialize(x, options)), options),
                ETA = eta,
                DateCreated = now,
                LastUpdated = now
            };
            step.Key = uniqueParameters 
                ? $"{step.ID}-{step.ActivityType}-{step.Method}-{step.Parameters}"
                : $"{step.ID}-{step.ActivityType}-{step.Method}-{step.DateCreated.Ticks}-{step.Parameters}"; 
            // step.ParametersHash = Convert.ToBase64String(sha.ComputeHash( System.Text.Encoding.UTF8.GetBytes(step.Parameters)));
            return step;
        }

        internal static ActivityStep Child(string parentID, string childId, DateTimeOffset eta, DateTimeOffset utcNow)
        {
            var step = new ActivityStep
            {
                ID = parentID,
                ActivityType = ActivityType.Child,
                Parameters = childId,
                ETA = eta,
                DateCreated = utcNow,
                LastUpdated = utcNow
            };
            step.Key = $"{step.ID}-{childId}";
            return step;
        }
    }
}
