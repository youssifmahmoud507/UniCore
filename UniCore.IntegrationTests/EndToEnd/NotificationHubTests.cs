using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using UniCore.Application.Modules.Notifications;
using UniCore.Infrastructure.Modules.Notifications;
using UniCore.IntegrationTests.Infrastructure;

namespace UniCore.IntegrationTests.EndToEnd
{
    [Collection(ApiCollection.Name)]
    public sealed class NotificationHubTests(ApiFixture fixture)
    {
        private HubConnection Connect(string? token) => new HubConnectionBuilder()
            .WithUrl($"http://localhost{NotificationHub.Path}", options =>
            {
                // The in-memory test server: long polling works through its handler, and the token goes in the
                // query string exactly like a browser WebSocket would send it.
                options.HttpMessageHandlerFactory = _ => fixture.CreateHandler();
                options.Transports = HttpTransportType.LongPolling;
                options.AccessTokenProvider = () => Task.FromResult(token);
            })
            .Build();

        private async Task PublishAsync(Guid userId, string title)
        {
            await using var scope = fixture.Services.CreateAsyncScope();
            var publisher = scope.ServiceProvider.GetRequiredService<INotificationPublisher>();

            var result = await publisher.PublishAsync(new NotificationRequest(userId, NotificationTypes.PaymentReceived, title, "Hub test"));

            Assert.True(result.IsSuccess);
        }

        [Fact]
        public async Task A_connected_person_receives_a_new_notification_at_once_with_the_unread_count()
        {
            var user = await fixture.Api.CreateUserAsync("hubuser");
            var token = await fixture.Api.LoginTokenAsync(user.UserName, user.Password);

            var received = new TaskCompletionSource<NotificationPush>(TaskCreationOptions.RunContinuationsAsynchronously);

            await using var connection = Connect(token);
            connection.On<NotificationPush>("notification", push => received.TrySetResult(push));
            await connection.StartAsync();

            await PublishAsync(user.Id, "Hello over SignalR");

            var push = await received.Task.WaitAsync(TimeSpan.FromSeconds(15));

            Assert.Equal("Hello over SignalR", push.Notification.Title);
            Assert.False(push.Notification.IsRead);
            Assert.Equal(1, push.UnreadCount);

            // The same notification is in the REST list: the database is the source of truth.
            var list = await fixture.Api.Client.GetAsync("/api/v1/me/notifications", token);
            Assert.Equal(1, list.Prop("totalCount").GetInt32());
        }

        [Fact]
        public async Task A_person_does_not_receive_what_was_sent_to_someone_else()
        {
            var listener = await fixture.Api.CreateUserAsync("hubother");
            var other = await fixture.Api.CreateUserAsync("hubtarget");
            var token = await fixture.Api.LoginTokenAsync(listener.UserName, listener.Password);

            var received = new TaskCompletionSource<NotificationPush>(TaskCreationOptions.RunContinuationsAsynchronously);

            await using var connection = Connect(token);
            connection.On<NotificationPush>("notification", push => received.TrySetResult(push));
            await connection.StartAsync();

            await PublishAsync(other.Id, "Not for you");

            var winner = await Task.WhenAny(received.Task, Task.Delay(TimeSpan.FromSeconds(3)));

            Assert.NotSame(received.Task, winner);
        }

        [Fact]
        public async Task The_hub_refuses_a_connection_without_a_valid_token()
        {
            await using var anonymous = Connect(null);
            await Assert.ThrowsAnyAsync<Exception>(() => anonymous.StartAsync());

            await using var forged = Connect("not-a-real-token");
            await Assert.ThrowsAnyAsync<Exception>(() => forged.StartAsync());
        }
    }
}
