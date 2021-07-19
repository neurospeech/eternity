using System;

namespace NeuroSpeech.Eternity
{
    public class EternityClock : IEternityClock
    {
        public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
    }

}
