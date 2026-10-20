using System;
using System.Collections.Generic;
using System.Text;
using UniCore.Domain.Modules.Notifications;

namespace UniCore.UnitTests.Modules.Notifications
{
    public class NotificationTests
    {
        private static readonly DateTimeOffset Now = new(2026, 10, 20, 10, 0, 0, TimeSpan.Zero);

        private static Notification New(string? type = "payment_received", string? title = "Payment received", string? content = "We received your payment.", Guid? source = null)
        {
            var result = Notification.Create(Guid.NewGuid(), type, title, content, " Invoice ", Guid.NewGuid(), source, false, Now);
            Assert.True(result.TryGetValue(out var notification));
            Assert.NotNull(notification);
            return notification;
        }

        [Fact]
        public void A_new_notification_is_unread_with_a_normalized_type_and_no_deliveries()
        {
            var notification = New();

            Assert.Equal("PAYMENT_RECEIVED", notification.Type);
            Assert.Equal("Invoice", notification.ReferenceType);
            Assert.False(notification.IsRead);
            Assert.False(notification.HasInApp);
            Assert.Empty(notification.Deliveries);
            Assert.Equal(Now, notification.CreatedAt);
        }

        [Fact]
        public void Bad_data_is_refused()
        {
            var id = Guid.NewGuid();

            Assert.True(Notification.Create(Guid.Empty, "PAYMENT_RECEIVED", "t", "c", null, null, null, false, Now).IsFailure);
            Assert.True(Notification.Create(id, "no", "t", "c", null, null, null, false, Now).IsFailure);
            Assert.True(Notification.Create(id, "bad type", "t", "c", null, null, null, false, Now).IsFailure);
            Assert.True(Notification.Create(id, "PAYMENT_RECEIVED", " ", "c", null, null, null, false, Now).IsFailure);
            Assert.True(Notification.Create(id, "PAYMENT_RECEIVED", "t", " ", null, null, null, false, Now).IsFailure);
            Assert.True(Notification.Create(id, "PAYMENT_RECEIVED", new string('t', Notification.MaxTitleLength + 1), "c", null, null, null, false, Now).IsFailure);
            Assert.True(Notification.Create(id, "PAYMENT_RECEIVED", "t", new string('c', Notification.MaxContentLength + 1), null, null, null, false, Now).IsFailure);
            Assert.True(Notification.Create(id, "PAYMENT_RECEIVED", "t", "c", new string('r', Notification.MaxReferenceTypeLength + 1), null, null, false, Now).IsFailure);
            Assert.True(Notification.Create(id, "PAYMENT_RECEIVED", "t", "c", null, Guid.Empty, null, false, Now).IsFailure);
            Assert.True(Notification.Create(id, "PAYMENT_RECEIVED", "t", "c", null, null, Guid.Empty, false, Now).IsFailure);
        }

        [Fact]
        public void A_blank_reference_type_is_stored_as_none()
        {
            var result = Notification.Create(Guid.NewGuid(), "PAYMENT_RECEIVED", "t", "c", "   ", null, null, false, Now);

            Assert.True(result.TryGetValue(out var notification));
            Assert.Null(notification!.ReferenceType);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("ab")]
        [InlineData("has space")]
        [InlineData("with-dash")]
        public void An_invalid_type_is_refused(string? type)
        {
            Assert.Null(Notification.NormalizeType(type));
        }

        [Fact]
        public void The_in_app_delivery_is_added_once()
        {
            var notification = New();

            notification.DeliverInApp(Now);
            notification.DeliverInApp(Now.AddMinutes(1));

            var delivery = Assert.Single(notification.Deliveries);
            Assert.Equal(NotificationChannel.InApp, delivery.Channel);
            Assert.Equal(NotificationDeliveryStatus.Sent, delivery.Status);
            Assert.Equal(Now, delivery.SentAt);
            Assert.True(notification.HasInApp);
        }

        [Fact]
        public void The_email_delivery_is_queued_or_failed_and_recorded_once()
        {
            var queued = New();
            queued.RecordEmail(NotificationDeliveryStatus.Queued, null, Now);
            queued.RecordEmail(NotificationDeliveryStatus.Failed, "later", Now);

            var delivery = Assert.Single(queued.Deliveries);
            Assert.Equal(NotificationDeliveryStatus.Queued, delivery.Status);
            Assert.Null(delivery.SentAt);
            Assert.Null(delivery.FailedAt);

            var failed = New();
            failed.RecordEmail(NotificationDeliveryStatus.Failed, new string('x', NotificationDelivery.MaxErrorLength + 50), Now);

            var broken = Assert.Single(failed.Deliveries);
            Assert.Equal(NotificationDeliveryStatus.Failed, broken.Status);
            Assert.Equal(Now, broken.FailedAt);
            Assert.Equal(NotificationDelivery.MaxErrorLength, broken.ErrorMessage!.Length);
            Assert.False(failed.HasInApp);
        }

        [Fact]
        public void Reading_twice_keeps_the_first_time()
        {
            var notification = New();

            notification.MarkRead(Now);
            notification.MarkRead(Now.AddHours(2));

            Assert.True(notification.IsRead);
            Assert.Equal(Now, notification.ReadAt);
        }

        [Fact]
        public void A_preference_is_created_updated_and_validated()
        {
            var user = Guid.NewGuid();

            Assert.True(NotificationPreference.Create(Guid.Empty, "PAYMENT_RECEIVED", true, true, Now).IsFailure);
            Assert.Equal(NotificationErrors.PreferenceInvalid.Code, NotificationPreference.Create(user, "no", true, true, Now).Error?.Code);

            var created = NotificationPreference.Create(user, "payment_received", false, true, Now);
            Assert.True(created.TryGetValue(out var preference));
            Assert.Equal("PAYMENT_RECEIVED", preference!.NotificationType);
            Assert.False(preference.InAppEnabled);
            Assert.True(preference.EmailEnabled);

            preference.Update(true, false, Now.AddMinutes(5));
            Assert.True(preference.InAppEnabled);
            Assert.False(preference.EmailEnabled);
            Assert.Equal(Now.AddMinutes(5), preference.UpdatedAt);
        }
    }

}
