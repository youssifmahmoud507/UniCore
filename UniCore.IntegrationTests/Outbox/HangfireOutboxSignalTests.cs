using Hangfire;
using Hangfire.Common;
using Hangfire.States;
using Microsoft.Extensions.Logging.Abstractions;
using UniCore.Infrastructure.Modules.Jobs;

namespace UniCore.IntegrationTests.Outbox
{
    public sealed class HangfireOutboxSignalTests
    {
        private sealed class CountingClient : IBackgroundJobClient
        {
            public int Created;

            // When true the client "crashes" like a Hangfire storage outage would.
            public bool Crash { get; set; }

            public string Create(Job job, IState state)
            {
                if (Crash)
                {
                    // A framework failure, not our own exception.
                    return Convert.ToInt32("storage is down").ToString();
                }

                return Interlocked.Increment(ref Created).ToString();
            }

            public bool ChangeState(string jobId, IState state, string expectedState) => true;
        }

        private static HangfireOutboxSignal NewSignal(CountingClient client) => new(client, NullLogger<HangfireOutboxSignal>.Instance);

        [Fact]
        public void Several_signals_queue_only_one_run()
        {
            var client = new CountingClient();
            var signal = NewSignal(client);

            signal.Signal();
            signal.Signal();
            signal.Signal();

            Assert.Equal(1, client.Created);
        }

        [Fact]
        public void After_the_job_starts_the_next_signal_queues_a_new_run()
        {
            var client = new CountingClient();
            var signal = NewSignal(client);

            signal.Signal();
            signal.Acknowledge();
            signal.Signal();

            Assert.Equal(2, client.Created);
        }

        [Fact]
        public void A_storage_failure_never_reaches_the_caller_and_the_next_signal_tries_again()
        {
            var client = new CountingClient { Crash = true };
            var signal = NewSignal(client);

            signal.Signal();
            Assert.Equal(0, client.Created);

            client.Crash = false;
            signal.Signal();

            Assert.Equal(1, client.Created);
        }
    }

}
