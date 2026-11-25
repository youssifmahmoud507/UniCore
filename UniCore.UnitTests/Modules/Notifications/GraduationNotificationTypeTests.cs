using UniCore.Application.Modules.Notifications;

namespace UniCore.UnitTests.Modules.Notifications
{
    public class GraduationNotificationTypeTests
    {
        [Fact]
        public void The_graduation_type_is_known_and_mandatory()
        {
            var info = NotificationTypes.Find("graduation_approved");

            Assert.NotNull(info);
            Assert.Equal(NotificationTypes.GraduationApproved, info.Code);
            Assert.True(info.Mandatory);
        }

        [Fact]
        public void Every_notification_type_code_is_unique()
        {
            var codes = NotificationTypes.All.Select(t => t.Code).ToList();

            Assert.Equal(codes.Count, codes.Distinct().Count());
        }
    }
}
