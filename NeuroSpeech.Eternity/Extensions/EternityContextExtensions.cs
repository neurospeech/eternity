using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NeuroSpeech.Eternity
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public class ScheduleDailyAttribute: Attribute
    {

        public static List<Type> GetTypes(Assembly[] assemblies)
        {
            var r = new List<Type>();
            foreach (var assembly in assemblies)
            {
                foreach (var type in assembly.GetExportedTypes())
                {
                    if (type.GetCustomAttribute<ScheduleDailyAttribute>() == null)
                        continue;
                    r.Add(type);
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

        /// <summary>
        /// Registers the tasks that will be executed daily
        /// </summary>
        /// <param name="context"></param>
        /// <param name="cancellationToken"></param>
        /// <param name="assemblies"></param>
        /// <param name="offset">TimeZone offset to adjust start of the day</param>
        /// <returns></returns>
        public static async Task RegisterDailyTasks(
            this EternityContext context,
            Assembly[] assemblies,
            CancellationToken cancellationToken,
            TimeSpan? offset
            )
        {
            try
            {
                var types = ScheduleDailyAttribute.GetTypes(assemblies);
                // in case if the task gets cancelled or errors out
                // we should recreate every hour..                
                var day = TimeSpan.FromHours(1);
                while (!cancellationToken.IsCancellationRequested)
                {
                    var now = DateTime.UtcNow;
                    if(offset != null)
                    {
                        now = now.Add(offset.Value);
                    }
                    now = now.Date;
                    foreach (var type in types)
                    {
                        var uniqueKey = $"{type.FullName}-{now.Ticks}";
                        await Generic.InvokeAs(type, ScheduleDaily<DailyWorkflow>, context, uniqueKey);
                    }
                    await Task.Delay(day, cancellationToken);
                }
            } catch (TaskCanceledException)
            {

            }
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        public static async Task ScheduleDaily<T>(
            this EternityContext context,
            string uniqueKey)
            where T: Workflow<T, string,string >
        {
            try
            {
                await Workflow<T, string, string>.CreateUniqueAsync(context,
                    input: uniqueKey,
                    id: uniqueKey);
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
