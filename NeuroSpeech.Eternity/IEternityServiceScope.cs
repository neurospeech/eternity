using System;

namespace NeuroSpeech.Eternity
{
    public interface IEternityServiceScope: IDisposable
    {
        IServiceProvider ServiceProvider {
            get;
        }
    }

}
