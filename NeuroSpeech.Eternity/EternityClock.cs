using System;

namespace NeuroSpeech.Eternity
{
    public class EternityClock : IEternityClock
    {
        public DateTimeOffset UtcNow => DateTimeOffset.UtcNow.TrimNanoSeconds();
    }

    internal static class DateTimeExtensions
    {
        public static DateTimeOffset TrimNanoSeconds(this DateTimeOffset dateTime)
        {
            return dateTime.AddTicks(-(dateTime.Ticks % TimeSpan.TicksPerMillisecond));
        }
    }

}
