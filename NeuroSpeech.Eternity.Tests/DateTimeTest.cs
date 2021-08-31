using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeuroSpeech.Eternity.Mocks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NeuroSpeech.Eternity.Tests
{

    public class ScheduleWorkflow : Workflow<ScheduleWorkflow, DateTime, string>
    {
        public override Task<string> RunAsync(DateTime input)
        {
            var at = input.Date.AddDays(1);
            return Print(at); 
        }

        [Activity(true)]
        public virtual Task<string> Print(DateTime at)
        {
            return Task.FromResult(at.ToString());
        }
    }

    [TestClass]
    public class DateTimeTest
    {

        [TestMethod]
        public async Task Test1()
        {
            var engine = new MockEngine();
            var context = engine.Resolve<EternityContext>();

            var d = new DateTime(2001, 1, 1);

            var w = await ScheduleWorkflow.CreateAsync(context, new WorkflowOptions<DateTime>
            {
                ID = d.ToShortDateString(),
                Input = d
            });

            engine.Clock.UtcNow += TimeSpan.FromMinutes(5);

            await context.ProcessMessagesOnceAsync();

            await context.ProcessMessagesOnceAsync();

            var r = await ScheduleWorkflow.GetStatusAsync(context, w);

            Assert.AreEqual(d.AddDays(1).ToString(), r.Result);
        }

    }
}
