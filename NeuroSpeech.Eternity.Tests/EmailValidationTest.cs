﻿using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeuroSpeech.Eternity.Mocks;
using NeuroSpeech.Eternity.Storage;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NeuroSpeech.Eternity.Tests
{
    public class SignupWorkflow : Workflow<SignupWorkflow, string, string>
    {
        public const string Resend = nameof(Resend);

        public const string Verify = nameof(Verify);

        public override async Task<string> RunAsync(string input)
        {
            var maxWait = TimeSpan.FromMinutes(15);
            var code = (this.CurrentUtc.Ticks & 0xF).ToString();
            await SendEmailAsync(input, code);
            for (int i = 0; i < 3; i++)
            {
                var (name, value) = await WaitForExternalEventsAsync(maxWait, Resend, Verify);
                switch(name)
                {
                    case Verify:
                        if(value == code)
                        {
                            return "Verified";
                        }
                        break;
                    case Resend:
                        await SendEmailAsync(input, code, i);
                        break;
                }
            }
            return "NotVerified";
        }

        [Activity]
        public virtual async Task<string> SendEmailAsync(
            string emailAddress, 
            string code, 
            int attempt = -1,
            [Inject] MockEmailService emailService = null) {
            await Task.Delay(100);
            emailService.Emails.Add((emailAddress, code, CurrentUtc));
            return $"{emailService.Emails.Count-1}";
        }
    }

    [TestClass]
    public class EmailValidationTest
    {

        [TestMethod]
        public async Task VerifyAsync()
        {
            var engine = new MockEngine();
            var emailService = engine.EmailService;
            var context = engine.Resolve<EternityContext>();

            // send email..
            var id = await SignupWorkflow.CreateAsync(context, "sample@gmail.com");

            engine.Clock.UtcNow += TimeSpan.FromMinutes(5);

            await context.ProcessMessagesOnceAsync();

            // check if we received the email..
            Assert.AreEqual(emailService.Emails.Count(), 1);

            var code = emailService.Emails[0].code;

            // fire event..
            await context.RaiseEventAsync(id, SignupWorkflow.Verify, code);

            engine.Clock.UtcNow += TimeSpan.FromMinutes(1);

            await context.ProcessMessagesOnceAsync();

            var status = await engine.Storage.GetAsync(id);

            Assert.AreEqual(status.State, EternityEntityState.Completed);

            Assert.AreEqual(status.Response, "\"Verified\"");


            var r = await SignupWorkflow.GetStatusAsync(context, id);
            Assert.IsNotNull(r);

            engine.Clock.UtcNow += TimeSpan.FromDays(60);

            await context.ProcessMessagesOnceAsync();

            await context.ProcessMessagesOnceAsync();

            r = await SignupWorkflow.GetStatusAsync(context, id);
            Assert.IsNull(r);
            // Assert.AreEqual(0, engine.Storage.QueueSize);
        }

        [TestMethod]
        public async Task ResendAsync()
        {
            var engine = new MockEngine();
            var emailService = engine.EmailService;
            var context = engine.Resolve<EternityContext>();

            // send email..
            var id = await SignupWorkflow.CreateAsync(context, "sample@gmail.com");

            engine.Clock.UtcNow += TimeSpan.FromMinutes(5);

            await context.ProcessMessagesOnceAsync();

            // check if we received the email..
            Assert.IsTrue(emailService.Emails.Any());

            var code = emailService.Emails[0].code;

            // fire event..
            await context.RaiseEventAsync(id, SignupWorkflow.Resend, null);

            engine.Clock.UtcNow += TimeSpan.FromMinutes(1);

            await context.ProcessMessagesOnceAsync();

            Assert.AreEqual(2, emailService.Emails.Count);

            await context.RaiseEventAsync(id, SignupWorkflow.Verify, code);

            engine.Clock.UtcNow += TimeSpan.FromMinutes(1);

            await context.ProcessMessagesOnceAsync();

            var status = await engine.Storage.GetAsync(id);

            Assert.AreEqual(status.State, EternityEntityState.Completed);

            Assert.AreEqual(status.Response, "\"Verified\"");

            engine.Clock.UtcNow += TimeSpan.FromDays(60);

            await context.ProcessMessagesOnceAsync();

           // Assert.AreEqual(0, engine.Storage.QueueSize);

        }

        [TestMethod]
        public async Task ResendAfterWrongCodeAsync()
        {
            var engine = new MockEngine();
            var emailService = engine.EmailService;
            var context = engine.Resolve<EternityContext>();

            // send email..
            var id = await SignupWorkflow.CreateAsync(context, "sample@gmail.com");

            engine.Clock.UtcNow += TimeSpan.FromMinutes(5);

            await context.ProcessMessagesOnceAsync();

            // check if we received the email..
            Assert.IsTrue(emailService.Emails.Any());

            var code = emailService.Emails[0].code;

            engine.Clock.UtcNow += TimeSpan.FromMinutes(1);
            await context.RaiseEventAsync(id, SignupWorkflow.Verify, code + "4");

            engine.Clock.UtcNow += TimeSpan.FromMinutes(1);

            await context.ProcessMessagesOnceAsync();

            // fire event..
            await context.RaiseEventAsync(id, SignupWorkflow.Resend, null);

            engine.Clock.UtcNow += TimeSpan.FromMinutes(1);

            await context.ProcessMessagesOnceAsync();

            Assert.AreEqual(2, emailService.Emails.Count);

            await context.RaiseEventAsync(id, SignupWorkflow.Verify, code);

            engine.Clock.UtcNow += TimeSpan.FromMinutes(1);

            await context.ProcessMessagesOnceAsync();

            var status = await engine.Storage.GetAsync(id);

            Assert.AreEqual(status.State, EternityEntityState.Completed);

            Assert.AreEqual(status.Response, "\"Verified\"");

            engine.Clock.UtcNow += TimeSpan.FromDays(60);

            await context.ProcessMessagesOnceAsync();

            // Assert.AreEqual(0, engine.Storage.QueueSize);

        }

        [TestMethod]
        public async Task TimedOut()
        {
            var engine = new MockEngine();
            var emailService = engine.EmailService;
            var context = engine.Resolve<EternityContext>();

            // send email..
            var id = await SignupWorkflow.CreateAsync(context, "sample@gmail.com");

            engine.Clock.UtcNow += TimeSpan.FromMinutes(5);

            await context.ProcessMessagesOnceAsync();

            // check if we received the email..
            Assert.IsTrue(emailService.Emails.Any());

            engine.Clock.UtcNow += TimeSpan.FromMinutes(20);

            await context.ProcessMessagesOnceAsync();


            engine.Clock.UtcNow += TimeSpan.FromMinutes(20);

            await context.ProcessMessagesOnceAsync();

            engine.Clock.UtcNow += TimeSpan.FromMinutes(20);

            await context.ProcessMessagesOnceAsync();

            var status = await engine.Storage.GetAsync(id);

            Assert.AreEqual(status.State, EternityEntityState.Completed);

            Assert.AreEqual(status.Response, "\"NotVerified\"");

            engine.Clock.UtcNow += TimeSpan.FromDays(60);

            await context.ProcessMessagesOnceAsync();

            // Assert.AreEqual(0, engine.Storage.QueueSize);

        }

    }
}
