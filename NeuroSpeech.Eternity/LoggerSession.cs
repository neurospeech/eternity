using System;
using System.Collections.Generic;
using System.Text;

namespace NeuroSpeech.Eternity
{
    public static class LoggerSessionHelper
    {
        public static LoggerSession? BeginLogSession(this IEternityLogger? logger)
            => logger == null
                ? null
                : new LoggerSession(logger);
    }

    public class LoggerSession : System.IDisposable
    {
        private readonly IEternityLogger logger;

        private List<(System.Diagnostics.TraceEventType Type, string Text)>? logs;

        public LoggerSession(IEternityLogger logger)
        {
            this.logger = logger;
        }

        public void Log(System.Diagnostics.TraceEventType type, string text)
        {
            (logs ??= new List<(System.Diagnostics.TraceEventType Type, string Text)>())
                .Add((type, text));
        }

        public void LogError(string text)
        {
            Log(System.Diagnostics.TraceEventType.Error, text);
        }
        public void LogVerbose(string text)
        {
            Log(System.Diagnostics.TraceEventType.Verbose, text);
        }
        public void LogInformation(string text)
        {
            Log(System.Diagnostics.TraceEventType.Information, text);
        }

        public void Dispose()
        {
            try {
                if(logs != null && logger != null)
                {
                    foreach(var (type,text) in logs)
                    {
                        logger.Log(type, text);
                    }
                }
            } catch(Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.ToString());
            }
        }
    }
}
