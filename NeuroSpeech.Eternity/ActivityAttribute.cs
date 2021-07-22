using System;

namespace NeuroSpeech.Eternity
{

    /// <summary>
    /// By default parameters are not considered as unique, set uniqueParameters to false to 
    /// consider current UTC as part
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public class ActivityAttribute: Attribute
    {
        public readonly bool UniqueParameters;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="uniqueParameters">
        /// When set to true, it will append CurrentUtc to unique parameters, instead 
        /// parameters combined as JSON will be used as unique key, irrespective in which
        /// sequence it is executed. Default is false.
        /// </param>
        public ActivityAttribute(bool uniqueParameters = false)
        {
            this.UniqueParameters = uniqueParameters;
        }

    }

    /// <summary>
    /// Use specified TimeSpan or DateTimeOffset to schedule the activity in future
    /// </summary>
    [AttributeUsage(AttributeTargets.Parameter)]
    public class ScheduleAttribute: Attribute
    {

    }
}
