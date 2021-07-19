using System;

namespace NeuroSpeech.Eternity
{
    public interface IServiceScopeFactory{
        IEternityServiceScope CreateScope(IServiceProvider services);
    }

}
