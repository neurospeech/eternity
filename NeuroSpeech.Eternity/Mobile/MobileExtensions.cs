using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace NeuroSpeech.Eternity.Mobile
{
    public static class MobileExtensions
    {

        public static Task<T> ScheduleAsync<T>(
                this IWorkflowObject workflow,
                Func<Task<T>> func
            )
        {
            var fx = func.Method;
            return workflow.InternalScheduleResultAsync<T>(fx.Name);
        }

        public static Task<T> ScheduleAsync<T, T1>(
                this IWorkflowObject workflow,
                Func<T1,Task<T>> func,
                T1? p1
            )
            
        {
            var fx = func.Method;
            return workflow.InternalScheduleResultAsync<T>(fx.Name, p1);
        }

        public static Task<T> ScheduleAsync<T, T1, T2>(
                this IWorkflowObject workflow,
                Func<T1, T2, Task<T>> func,
                T1? p1,
                T2? p2
            )
            
        {
            var fx = func.Method;
            return workflow.InternalScheduleResultAsync<T>(fx.Name, p1, p2);
        }

        public static Task<T> ScheduleAsync<T, T1, T2, T3>(
                this IWorkflowObject workflow,
                Func<T1, T2, T3, Task<T>> func,
                T1? p1,
                T2? p2,
                T3? p3
            )
            
        {
            var fx = func.Method;
            return workflow.InternalScheduleResultAsync<T>(fx.Name, p1, p2, p3);
        }

        public static Task<T> ScheduleAsync<T, T1, T2, T3, T4>(
                this IWorkflowObject workflow,
                Func<T1, T2, T3, T4, Task<T>> func,
                T1? p1,
                T2? p2,
                T3? p3,
                T4? p4
            )
            
        {
            var fx = func.Method;
            return workflow.InternalScheduleResultAsync<T>(fx.Name, p1, p2, p3, p4);
        }

        public static Task<T> ScheduleAsync<T, T1, T2, T3, T4, T5>(
                this IWorkflowObject workflow,
                Func<T1, T2, T3, T4, T5, Task<T>> func,
                T1? p1,
                T2? p2,
                T3? p3,
                T4? p4,
                T5? p5
            )
            
        {
            var fx = func.Method;
            return workflow.InternalScheduleResultAsync<T>(fx.Name, p1, p2, p3, p4, p5);
        }

        public static Task<T> ScheduleAsync<T, T1, T2, T3, T4, T5, T6>(
            this IWorkflowObject workflow,
            Func<T1, T2, T3, T4, T5, T6, Task<T>> func,
            T1? p1,
            T2? p2,
            T3? p3,
            T4? p4,
            T5? p5,
            T6? p6
        )
        
        {
            var fx = func.Method;
            return workflow.InternalScheduleResultAsync<T>(fx.Name, p1, p2, p3, p4, p5, p6);
        }

        public static Task<T> ScheduleAsync<T, T1, T2, T3, T4, T5, T6, T7>(
            this IWorkflowObject workflow,
            Func<T1, T2, T3, T4, T5, T6, T7, Task<T>> func,
            T1? p1,
            T2? p2,
            T3? p3,
            T4? p4,
            T5? p5,
            T6? p6,
            T7? p7
        )
        
        {
            var fx = func.Method;
            return workflow.InternalScheduleResultAsync<T>(fx.Name, p1, p2, p3, p4, p5, p6, p7);
        }

        public static Task<T> ScheduleAsync<T, T1, T2, T3, T4, T5, T6, T7, T8>(
            this IWorkflowObject workflow,
            Func<T1, T2, T3, T4, T5, T6, T7, T8, Task<T>> func,
            T1? p1,
            T2? p2,
            T3? p3,
            T4? p4,
            T5? p5,
            T6? p6,
            T7? p7,
            T8? p8
        )
        
        {
            var fx = func.Method;
            return workflow.InternalScheduleResultAsync<T>(fx.Name, p1, p2, p3, p4, p5, p6, p7, p8);
        }

        public static Task<T> ScheduleAsync<T, TW, TInput, TOutput>(
                this IWorkflowObject workflow,
                TimeSpan ts,
                Func<Task<T>> func
            )
            
        {
            var fx = func.Method;
            return workflow.InternalScheduleAfterResultAsync<T>(ts, fx.Name);
        }

        public static Task<T> ScheduleAsync<T, T1>(
                this IWorkflowObject workflow,
                TimeSpan ts,
                Func<T1, Task<T>> func,
                T1? p1
            )
            
        {
            var fx = func.Method;
            return workflow.InternalScheduleAfterResultAsync<T>(ts, fx.Name, p1);
        }

        public static Task<T> ScheduleAsync<T, T1, T2>(
                this IWorkflowObject workflow,
                TimeSpan ts,
                Func<T1, T2, Task<T>> func,
                T1? p1,
                T2? p2
            )
            
        {
            var fx = func.Method;
            return workflow.InternalScheduleAfterResultAsync<T>(ts, fx.Name, p1, p2);
        }

        public static Task<T> ScheduleAsync<T, T1, T2, T3>(
                this IWorkflowObject workflow,
                TimeSpan ts,
                Func<T1, T2, T3, Task<T>> func,
                T1? p1,
                T2? p2,
                T3? p3
            )
            
        {
            var fx = func.Method;
            return workflow.InternalScheduleAfterResultAsync<T>(ts, fx.Name, p1, p2, p3);
        }

        public static Task<T> ScheduleAsync<T, T1, T2, T3, T4>(
                this IWorkflowObject workflow,
                TimeSpan ts,
                Func<T1, T2, T3, T4, Task<T>> func,
                T1? p1,
                T2? p2,
                T3? p3,
                T4? p4
            )
            
        {
            var fx = func.Method;
            return workflow.InternalScheduleAfterResultAsync<T>(ts, fx.Name, p1, p2, p3, p4);
        }

        public static Task<T> ScheduleAsync<T, T1, T2, T3, T4, T5>(
                this IWorkflowObject workflow,
                TimeSpan ts,
                Func<T1, T2, T3, T4, T5, Task<T>> func,
                T1? p1,
                T2? p2,
                T3? p3,
                T4? p4,
                T5? p5
            )
            
        {
            var fx = func.Method;
            return workflow.InternalScheduleAfterResultAsync<T>(ts, fx.Name, p1, p2, p3, p4, p5);
        }

        public static Task<T> ScheduleAsync<T, T1, T2, T3, T4, T5, T6>(
            this IWorkflowObject workflow,
                TimeSpan ts,
            Func<T1, T2, T3, T4, T5, T6, Task<T>> func,
            T1? p1,
            T2? p2,
            T3? p3,
            T4? p4,
            T5? p5,
            T6? p6
        )
        
        {
            var fx = func.Method;
            return workflow.InternalScheduleAfterResultAsync<T>(ts, fx.Name, p1, p2, p3, p4, p5, p6);
        }

        public static Task<T> ScheduleAsync<T, T1, T2, T3, T4, T5, T6, T7>(
            this IWorkflowObject workflow,
                TimeSpan ts,
            Func<T1, T2, T3, T4, T5, T6, T7, Task<T>> func,
            T1? p1,
            T2? p2,
            T3? p3,
            T4? p4,
            T5? p5,
            T6? p6,
            T7? p7
        )
        
        {
            var fx = func.Method;
            return workflow.InternalScheduleAfterResultAsync<T>(ts, fx.Name, p1, p2, p3, p4, p5, p6, p7);
        }

        public static Task<T> ScheduleAsync<T, T1, T2, T3, T4, T5, T6, T7, T8>(
            this IWorkflowObject workflow,
                TimeSpan ts,
            Func<T1, T2, T3, T4, T5, T6, T7, T8, Task<T>> func,
            T1? p1,
            T2? p2,
            T3? p3,
            T4? p4,
            T5? p5,
            T6? p6,
            T7? p7,
            T8? p8
        )
        
        {
            var fx = func.Method;
            return workflow.InternalScheduleAfterResultAsync<T>(ts, fx.Name, p1, p2, p3, p4, p5, p6, p7, p8);
        }


    }
}
