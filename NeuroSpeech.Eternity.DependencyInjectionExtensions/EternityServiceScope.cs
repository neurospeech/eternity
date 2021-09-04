using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Text;

namespace NeuroSpeech.Eternity
{

    public class EternityServiceScopeFactory : IServiceScopeFactory
    {
        public IEternityServiceScope CreateScope(IServiceProvider services)
        {
            return new Scope(services.CreateScope());
        }

        class Scope : IEternityServiceScope
        {
            private IServiceScope serviceScope;

            public Scope(IServiceScope serviceScope)
            {
                this.serviceScope = serviceScope;
            }

            public IServiceProvider ServiceProvider => serviceScope.ServiceProvider;

            public void Dispose()
            {
                serviceScope.Dispose();
            }
        }
    }
}
