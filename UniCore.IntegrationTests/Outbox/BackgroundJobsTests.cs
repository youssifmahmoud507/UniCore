using Hangfire;
using Hangfire.Storage;
using Microsoft.Extensions.DependencyInjection;
using UniCore.Application.Common.Abstractions;
using UniCore.Infrastructure.Modules.Jobs;
using UniCore.Infrastructure.Modules.Outbox;
using UniCore.IntegrationTests.Infrastructure;

namespace UniCore.IntegrationTests.Outbox
{
    [Collection(ApiCollection.Name)]
    public sealed class BackgroundJobsTests(ApiFixture fixture)
    {
        [Fact]
        public void The_outbox_job_is_registered_to_run_every_minute()
        {
            var storage = fixture.Services.GetRequiredService<JobStorage>();

            using var connection = storage.GetConnection();
            var job = Assert.Single(connection.GetRecurringJobs(), j => j.Id == OutboxProcessingJob.RecurringJobId);

            Assert.Equal("* * * * *", job.Cron);
        }

        [Fact]
        public void The_api_uses_the_outbox_for_email_and_the_hangfire_signal()
        {
            Assert.IsType<OutboxEmailQueue>(fixture.Services.GetRequiredService<IEmailQueue>());
            Assert.IsType<HangfireOutboxSignal>(fixture.Services.GetRequiredService<IOutboxSignal>());
        }

        [Fact]
        public async Task The_hangfire_dashboard_is_off_by_default()
        {
            var response = await fixture.Api.Client.GetAsync(BackgroundJobsExtensions.DashboardPath);

            Assert.Equal(404, response.Code);
        }
    }

}
