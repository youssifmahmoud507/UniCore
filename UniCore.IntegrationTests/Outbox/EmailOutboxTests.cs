using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using UniCore.Domain.Modules.Outbox;
using UniCore.Infrastructure.Modules.Outbox;
using UniCore.Infrastructure.Persistence;
using UniCore.IntegrationTests.Infrastructure;

namespace UniCore.IntegrationTests.Outbox
{
    [Collection(ApiCollection.Name)]
    public sealed class EmailOutboxTests(ApiFixture fixture)
    {
        private readonly TestApi _api = fixture.Api;

        private async Task<List<OutboxMessage>> EmailRowsAsync(Func<IQueryable<OutboxMessage>, IQueryable<OutboxMessage>> filter)
        {
            await using var scope = fixture.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            return await filter(db.Set<OutboxMessage>().AsNoTracking().Where(m => m.Type == EmailOutboxHandler.EmailMessageType)).ToListAsync();
        }

        [Fact]
        public async Task The_reset_code_email_is_delivered_and_the_code_does_not_stay_in_the_database()
        {
            var user = await _api.CreateUserAsync("emailoutbox", setPassword: false);

            var otp = await _api.RequestOtpAsync(user.Email);

            var wiped = false;
            var deadline = DateTimeOffset.UtcNow.AddSeconds(15);

            while (DateTimeOffset.UtcNow < deadline && !wiped)
            {
                wiped = (await EmailRowsAsync(q => q.Where(m => m.Payload.Contains(otp)))).Count == 0;

                if (!wiped)
                {
                    await Task.Delay(200);
                }
            }

            Assert.True(wiped);
        }

        [Fact]
        public async Task A_delivered_email_keeps_no_payload_and_has_a_processed_time()
        {
            var user = await _api.CreateUserAsync("emaildone", setPassword: false);
            var skip = _api.Emails.Count;

            await _api.Client.PostAsync("/api/v1/auth/forgot-password", new { email = user.Email });
            var mail = await _api.Emails.WaitForAsync(m => m.To == user.Email, skip, TimeSpan.FromSeconds(15));
            Assert.NotNull(mail);

            var deadline = DateTimeOffset.UtcNow.AddSeconds(15);
            List<OutboxMessage> processed = [];

            while (DateTimeOffset.UtcNow < deadline)
            {
                processed = await EmailRowsAsync(q => q.Where(m => m.Status == OutboxMessageStatus.Processed));

                if (processed.Count > 0)
                {
                    break;
                }

                await Task.Delay(200);
            }

            Assert.NotEmpty(processed);
            Assert.All(processed, m =>
            {
                Assert.Equal(OutboxMessage.RedactedPayload, m.Payload);
                Assert.NotNull(m.ProcessedAt);
            });
        }
    }

}
