using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace NeuroSpeech.Eternity
{
    internal static class GeneratedMethodHelper
    {

        private static MethodInfo methodRunAsyncOfT = typeof(GeneratedMethodHelper).GetMethod(nameof(InvokeAsyncOfT));

        public static async Task<string> InvokeAsyncOfT<T>(
            MethodInfo method,
            object target,
            object?[] parameters,
            System.Text.Json.JsonSerializerOptions? options = default)
        {
            var r = (await (method.Invoke(target, parameters) as Task<T>)!);
            return JsonSerializer.Serialize(r, options);
        }

        public static async Task<string> InvokeAsync(
            this MethodInfo method,
            object target,
            object?[] parameters,
            System.Text.Json.JsonSerializerOptions? options = null)
        {
            if (method.ReturnType.IsConstructedGenericType)
            {
                var returnType = method.ReturnType.GenericTypeArguments[0];

                return await (methodRunAsyncOfT.MakeGenericMethod(returnType).Invoke(target, new object?[] {
                    method,
                    target,
                    parameters,
                    options
                }) as Task<string>)!;
            }

            await (method.Invoke(target, parameters) as Task)!;
            return "null";
        }
    }
}
