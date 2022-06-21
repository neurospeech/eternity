using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeuroSpeech.Eternity.Mocks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NeuroSpeech.Eternity.Tests
{

    public class ParentWorkflow : Workflow<ParentWorkflow, int, int>
    {
        public override Task<int> RunAsync(int input)
        {
            return ChildWorkflow.ExecuteAsync(this, input);
        }
    }

    public class ChildWorkflow : Workflow<ChildWorkflow, int, int>
    {
        public override async Task<int> RunAsync(int input)
        {
            var at = TimeSpan.FromSeconds(1);
            input = await Add(at, input);
            input = await Add(at, input);
            input = await Add(at, input);
            return input;
        }

        [Activity(true)]
        public virtual async Task<int> Add(
            [Schedule] TimeSpan at, int i)
        {
            await Task.Delay(100);
            return i + i;
        }
    }

    [TestClass]
    public class CompositeTests
    {

        [TestMethod]
        public async Task Run()
        {
            var engine = new MockEngine();
            var context = engine.Resolve<EternityContext>();
            var id = await ParentWorkflow.CreateAsync(context, 1);

            await context.ProcessMessagesOnceAsync();

            engine.Clock.UtcNow += TimeSpan.FromSeconds(30);

            await context.ProcessMessagesOnceAsync();

            engine.Clock.UtcNow += TimeSpan.FromSeconds(30);

            await context.ProcessMessagesOnceAsync();

            engine.Clock.UtcNow += TimeSpan.FromSeconds(30);

            await context.ProcessMessagesOnceAsync();

            var s = await ParentWorkflow.GetStatusAsync(context, id);

            Assert.AreEqual(s.Status, EternityEntityState.Completed);
            Assert.AreEqual(s.Result, 8);
        }

    }
}
