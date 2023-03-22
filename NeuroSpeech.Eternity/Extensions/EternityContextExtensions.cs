using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NeuroSpeech.Eternity
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public class ScheduleDailyAttribute: Attribute
    {

        public string[] Groups { get; set; }

        /// <summary>
        /// If empty, group is "Default"
        /// </summary>
        /// <param name="groups"></param>
        public ScheduleDailyAttribute(params string[] groups)
        {
            this.Groups = groups.Length > 0 ? groups : new string[] { "Default" };
        }

        public static List<(Type type,string[] group)> GetTypes(Assembly[] assemblies)
        {
            var r = new List<(Type,string[])>();
            foreach (var assembly in assemblies)
            {
                foreach (var type in assembly.GetExportedTypes())
                {
                    var s = type.GetCustomAttribute<ScheduleDailyAttribute>();
                    if (s == null)
                        continue;
                    r.Add((type, s.Groups));
                }
            }
            return r;
        }

    }

    public class DailyWorkflow : Workflow<DailyWorkflow, string, string>
    {
        public DailyWorkflow()
        {
            this.PreserveTime = TimeSpan.FromHours(26);
            this.FailurePreserveTime = TimeSpan.FromHours(1);
        }

        public override Task<string> RunAsync(string input)
        {
            return Task.FromResult("None");
        }
    }

    public static class EternityContextExtensions
    {

        internal class DailyWorkflowTypes
        {
            private readonly ConcurrentDictionary<string, List<Type>> types;

            public DailyWorkflowTypes()
            {
                this.types = new ConcurrentDictionary<string, List<Type>>();
            }

            public List<Type> ForGroup(string group = "Default")
            {
                return types.GetOrAdd(group, (k) => new List<Type>());
            }

            public void AddRange(List<(Type, string[])> types)
            {
                foreach(var (type, groups) in types)
                {
                    foreach(var group in groups)
                    {
                        ForGroup(group).Add(type);
                    }
                }
            }
        }

        internal static DailyWorkflowTypes GetDailyWorkflowTypes(
            this IServiceCollection services)
        {
            var s = services.FirstOrDefault(x => x.ServiceType == typeof(DailyWorkflowTypes));
            if (s == null)
            {
                s = new ServiceDescriptor(typeof(DailyWorkflowTypes), new DailyWorkflowTypes());
                services.Add(s);
            }
            return (s.ImplementationInstance as DailyWorkflowTypes)!;
        }

        public static void RegisterDailyWorkflows(
            this IServiceCollection services,
            params Assembly[] assemblies
            )
        {
            var types = ScheduleDailyAttribute.GetTypes(assemblies);
            services.GetDailyWorkflowTypes().AddRange(types);
        }

        public static void RegisterDailyWorkflows(
            this IServiceCollection services,
            Type type,
            string group = "Default"
            )
        {
            services.GetDailyWorkflowTypes().ForGroup(group).Add(type);
        }

        public static void RegisterDailyWorkflow<T>(
            this IServiceCollection services,
            string group
            )
        {
            services.GetDailyWorkflowTypes().ForGroup(group).Add(typeof(T));
        }

        public static void RunDailyWorkflows(
            this EternityContext context,
            CancellationToken cancellationToken,
            string group = "Default",
            TimeSpan? offset = null)
        {
            Task.Run(async () => {
                var types = context.services.GetRequiredService<DailyWorkflowTypes>().ForGroup(group);
                try
                {
                    var delay = TimeSpan.FromHours(1);
                    while (!cancellationToken.IsCancellationRequested)
                    {
                        var now = DateTime.UtcNow;
                        if (offset != null)
                        {
                            now = now.Add(offset.Value);
                        }
                        now = now.Date;
                        foreach (var type in types)
                        {
                            var uniqueKey = $"{type.FullName}-{now:O}";
                            await Generic.InvokeAs(type, ScheduleDaily<DailyWorkflow>, context, uniqueKey);
                        }
                        await Task.Delay(delay, cancellationToken);
                    }
                }catch (TaskCanceledException)
                {

                }
            });
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        public static async Task ScheduleDaily<T>(
            this EternityContext context,
            string uniqueKey)
            where T: DailyWorkflow
        {
            try
            {
                await context.CreateAsync(typeof(T), new WorkflowOptions<string> {
                    ID = uniqueKey,
                    Input = uniqueKey
                }, throwIfExists: false );
            }catch (Exception ex)
            {
                context.logger?.LogError(ex.ToString());
            }
        }

    }

    internal static class Generic
    {
        private static ConcurrentDictionary<(Type type1, Type type2, MethodInfo method), object> cache
            = new ConcurrentDictionary<(Type, Type, MethodInfo), object>();

        private static T CreateTypedDelegate<T>(this MethodInfo method)
            where T : Delegate
        {
            return (T)method.CreateDelegate(typeof(T));
        }

        private static T TypedGet<T>(
            (Type, Type, MethodInfo) key,
            Func<(Type type1, Type type2, MethodInfo method), T> create)
        {
            return (T)cache.GetOrAdd(key, (x) => create(x));
        }

        public static T InvokeAs<T>(Type type, Func<T> fx)
        {
            var method = TypedGet(
                    (type, type, fx.Method),
                    (k) => k.method
                        .GetGenericMethodDefinition()
                        .MakeGenericMethod(k.type1)
                        .CreateTypedDelegate<Func<T>>());
            return method();
        }

        public static T InvokeAs<T1, T>(Type type, Func<T1, T> fx, T1 p1)
        {
            var method = TypedGet(
                    (type, type, fx.Method),
                    (k) => k.method
                        .GetGenericMethodDefinition()
                        .MakeGenericMethod(k.type1)
                        .CreateTypedDelegate<Func<T1, T>>());
            return method(p1);
        }

        public static T InvokeAs<T1, T2, T>(Type type, Func<T1, T2, T> fx, T1 p1, T2 p2)
        {
            var method = TypedGet(
                    (type, type, fx.Method),
                    (k) => k.method
                        .GetGenericMethodDefinition()
                        .MakeGenericMethod(k.type1)
                        .CreateTypedDelegate<Func<T1, T2, T>>());
            return method(p1, p2);
        }
        public static T InvokeAs<T1, T2, T3, T>(Type type, Func<T1, T2, T3, T> fx, T1 p1, T2 p2, T3 p3)
        {
            var method = TypedGet(
                    (type, type, fx.Method),
                    (k) => k.method
                        .GetGenericMethodDefinition()
                        .MakeGenericMethod(k.type1)
                        .CreateTypedDelegate<Func<T1, T2, T3, T>>());
            return method(p1, p2, p3);
        }

        public static T InvokeAs<Target, T>(this Target target, Type type, Func<T> fx)
        {
            var method = TypedGet(
                    (type, type, fx.Method),
                    (k) => k.method
                        .GetGenericMethodDefinition()
                        .MakeGenericMethod(k.type1)
                        .CreateTypedDelegate<Func<Target, T>>());
            return method(target);
        }

        public static T InvokeAs<Target, T1, T>(this Target target, Type type, Func<T1, T> fx, T1 p1)
        {
            var method = TypedGet(
                    (type, type, fx.Method),
                    (k) => k.method
                        .GetGenericMethodDefinition()
                        .MakeGenericMethod(k.type1)
                        .CreateTypedDelegate<Func<Target, T1, T>>());
            return method(target, p1);
        }

        public static T InvokeAs<Target, T1, T2, T>(this Target target, Type type, Func<T1, T2, T> fx, T1 p1, T2 p2)
        {
            var method = TypedGet(
                    (type, type, fx.Method),
                    (k) => k.method
                        .GetGenericMethodDefinition()
                        .MakeGenericMethod(k.type1)
                        .CreateTypedDelegate<Func<Target, T1, T2, T>>());
            return method(target, p1, p2);
        }
        public static T InvokeAs<Target, T1, T2, T3, T>(this Target target, Type type, Func<T1, T2, T3, T> fx, T1 p1, T2 p2, T3 p3)
        {
            var method = TypedGet(
                    (type, type, fx.Method),
                    (k) => k.method
                        .GetGenericMethodDefinition()
                        .MakeGenericMethod(k.type1)
                        .CreateTypedDelegate<Func<Target, T1, T2, T3, T>>());
            return method(target, p1, p2, p3);
        }

        public static T InvokeAs<Target, T>(this Target target, Type type, Type type2, Func<T> fx)
        {
            var method = TypedGet(
                    (type, type2, fx.Method),
                    (k) => k.method
                        .GetGenericMethodDefinition()
                        .MakeGenericMethod(k.type1, k.type2)
                        .CreateTypedDelegate<Func<Target, T>>());
            return method(target);
        }

        public static T InvokeAs<Target, T1, T>(this Target target, Type type, Type type2, Func<T1, T> fx, T1 p1)
        {
            var method = TypedGet(
                    (type, type2, fx.Method),
                    (k) => k.method
                        .GetGenericMethodDefinition()
                        .MakeGenericMethod(k.type1, k.type2)
                        .CreateTypedDelegate<Func<Target, T1, T>>());
            return method(target, p1);
        }

        public static T InvokeAs<Target, T1, T2, T>(this Target target, Type type, Type type2, Func<T1, T2, T> fx, T1 p1, T2 p2)
        {
            var method = TypedGet(
                    (type, type2, fx.Method),
                    (k) => k.method
                        .GetGenericMethodDefinition()
                        .MakeGenericMethod(k.type1, k.type2)
                        .CreateTypedDelegate<Func<Target, T1, T2, T>>());
            return method(target, p1, p2);
        }
        public static T InvokeAs<Target, T1, T2, T3, T>(this Target target, Type type, Type type2, Func<T1, T2, T3, T> fx, T1 p1, T2 p2, T3 p3)
        {
            var method = TypedGet(
                    (type, type2, fx.Method),
                    (k) => k.method
                        .GetGenericMethodDefinition()
                        .MakeGenericMethod(k.type1, k.type2)
                        .CreateTypedDelegate<Func<Target, T1, T2, T3, T>>());
            return method(target, p1, p2, p3);
        }
    }


}
