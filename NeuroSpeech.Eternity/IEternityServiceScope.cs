using System;

namespace NeuroSpeech.Eternity
{
    public interface IEternityServiceScope: IDisposable
    {
        IServiceProvider ServiceProvider {
            get;
        }
    }

    public static class EternityServiceScopeExtensions
    {
        public static T Resolve<T>(this IEternityServiceScope scope)
        {
            return scope.ServiceProvider.GetService(typeof(T)) is T item
                ? item
                : throw new ArgumentException($"Unable to resolve {typeof(T).FullName}");
        }
    }

}
