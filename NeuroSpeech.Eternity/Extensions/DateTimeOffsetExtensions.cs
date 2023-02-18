using System;
using System.Collections.Generic;
using System.Text;

namespace NeuroSpeech.Eternity
{
    public static class DateTimeOffsetExtensions
    {

        public static DateTimeOffset ToSqlServerPrecision(this DateTimeOffset dt)
        {
            var n = Math.Round((double)dt.ToUnixTimeMilliseconds() / (double)1000, 2);
            n = n * 1000;
            return DateTimeOffset.FromUnixTimeMilliseconds((long)n);
        }

        public static DateTime ToSqlServerPrecision(this DateTime @this)
        {
            var dt = new DateTimeOffset(@this, TimeSpan.Zero);
            var n = Math.Round((double)dt.ToUnixTimeMilliseconds() / (double)1000, 2);
            n = n * 1000;
            var r = DateTimeOffset.FromUnixTimeMilliseconds((long)n);
            return new DateTime(r.Ticks, @this.Kind);
        }
    }
}
