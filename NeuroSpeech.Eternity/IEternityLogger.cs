using System;
using System.Collections.Generic;
using System.Text;

namespace NeuroSpeech.Eternity
{
    public interface IEternityLogger
    {
        void Log(System.Diagnostics.TraceEventType type, string text);
    }
}
