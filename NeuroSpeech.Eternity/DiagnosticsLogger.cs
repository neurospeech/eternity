using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace NeuroSpeech.Eternity
{
    public class DiagnosticsLogger : IEternityLogger
    {
        public void Log(TraceEventType type, string text)
        {
            System.Diagnostics.Debug.WriteLine($"{type}: {text}");
        }    
    }
}
