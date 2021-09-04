using Microsoft.Extensions.DependencyInjection;

namespace NeuroSpeech.Eternity
{
    public static class EternityServiceScopeExtensions
    {
        public static void AddEternityServiceScope(this IServiceCollection services)
        {
            services.AddSingleton(typeof(IServiceScopeFactory), typeof(EternityServiceScopeFactory));
        }
    }
}
