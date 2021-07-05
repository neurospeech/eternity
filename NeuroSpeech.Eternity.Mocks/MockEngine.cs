using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Text;

namespace NeuroSpeech.Eternity.Mocks
{
    public class MockEngine : MockEngine<MockStorage>
    {
        public MockEngine(Action<IServiceCollection> builder = null)
            :base(builder)
        {
        }

        public override void Dispose()
        {
            
        }

        protected override MockStorage CreateStorage(MockClock clock)
        {
            return new MockStorage(clock);
        }
    }

    public abstract class MockEngine<TStorage>: IDisposable
        where TStorage: IEternityStorage
    {

        public MockEngine(Action<IServiceCollection> builder = null)
        {
            Clock = new MockClock();
            Storage = CreateStorage(Clock);
            Bag = new MockBag();
            EmailService = new MockEmailService();
            ServiceCollection services = new ServiceCollection();
            services.AddSingleton<IEternityClock>(Clock);
            services.AddSingleton(Bag);
            services.AddSingleton<IEternityStorage>(Storage);
            services.AddSingleton(EmailService);
            services.AddSingleton<EternityContext>();
            builder?.Invoke(services);
            this.Services = services.BuildServiceProvider();
        }

        protected abstract TStorage CreateStorage(MockClock clock);

        public readonly IServiceProvider Services;

        public readonly MockBag Bag;

        public readonly MockClock Clock;

        public readonly TStorage Storage;

        public readonly MockEmailService EmailService;

        public T Resolve<T>()
        {
            return Services.GetRequiredService<T>();
        }

        public abstract void Dispose();
    }
}
