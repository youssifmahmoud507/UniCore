using UniCore.Domain.Modules.Courses;
using UniCore.UnitTests.Common;

namespace UniCore.UnitTests.Modules.Courses
{
    public class CourseVersionTests
    {
        private static readonly DateTimeOffset Now = new(2026, 10, 12, 9, 0, 0, TimeSpan.Zero);
        private static readonly Guid CourseId = Guid.NewGuid();
        private static readonly DateOnly From = new(2026, 9, 1);

        private static CourseVersion NewDraft() => ResultAssert.Succeeded(CourseVersion.Create(CourseId, 1, "Intro to Programming", null, 3, 2, 2, From, Now));

        [Fact]
        public void Create_starts_as_a_draft()
        {
            var version = ResultAssert.Succeeded(CourseVersion.Create(CourseId, 1, " Intro ", " Basics ", 3, 2, 2, From, Now));

            Assert.NotEqual(Guid.Empty, version.Id);
            Assert.Equal(CourseId, version.CourseId);
            Assert.Equal(1, version.VersionNumber);
            Assert.Equal("Intro", version.Title);
            Assert.Equal("Basics", version.Description);
            Assert.Equal(3, version.Credits);
            Assert.Equal(2, version.LectureHours);
            Assert.Equal(2, version.PracticalHours);
            Assert.Equal(From, version.EffectiveFrom);
            Assert.Null(version.EffectiveTo);
            Assert.Equal(CourseVersionStatus.Draft, version.Status);
        }

        [Fact]
        public void Create_rejects_bad_input()
        {
            ResultAssert.Failed(CourseVersion.Create(Guid.Empty, 1, "Intro", null, 3, 2, 2, From, Now), CoursesErrors.InvalidParent);
            ResultAssert.Failed(CourseVersion.Create(CourseId, 0, "Intro", null, 3, 2, 2, From, Now), CoursesErrors.VersionNumberInvalid);
            ResultAssert.Failed(CourseVersion.Create(CourseId, 1, "Intro", null, 3, 2, 2, default, Now), CoursesErrors.EffectiveDateInvalid);
            ResultAssert.Failed(CourseVersion.Create(CourseId, 1, " ", null, 3, 2, 2, From, Now), CoursesErrors.InvalidTitle);
            ResultAssert.Failed(CourseVersion.Create(CourseId, 1, "Intro", new string('x', 1001), 3, 2, 2, From, Now), CoursesErrors.InvalidDescription);
            ResultAssert.Failed(CourseVersion.Create(CourseId, 1, "Intro", null, null, 2, 2, From, Now), CoursesErrors.CreditsInvalid);
            ResultAssert.Failed(CourseVersion.Create(CourseId, 1, "Intro", null, 0, 2, 2, From, Now), CoursesErrors.CreditsInvalid);
            ResultAssert.Failed(CourseVersion.Create(CourseId, 1, "Intro", null, 31, 2, 2, From, Now), CoursesErrors.CreditsInvalid);
            ResultAssert.Failed(CourseVersion.Create(CourseId, 1, "Intro", null, 3, null, 2, From, Now), CoursesErrors.HoursInvalid);
            ResultAssert.Failed(CourseVersion.Create(CourseId, 1, "Intro", null, 3, 2, -1, From, Now), CoursesErrors.HoursInvalid);
            ResultAssert.Failed(CourseVersion.Create(CourseId, 1, "Intro", null, 3, 61, 0, From, Now), CoursesErrors.HoursInvalid);
            ResultAssert.Failed(CourseVersion.Create(CourseId, 1, "Intro", null, 3, 0, 0, From, Now), CoursesErrors.HoursInvalid);
        }

        [Fact]
        public void A_course_can_be_all_lecture_or_all_practical()
        {
            ResultAssert.Succeeded(CourseVersion.Create(CourseId, 1, "Intro", null, 3, 3, 0, From, Now));
            ResultAssert.Succeeded(CourseVersion.Create(CourseId, 1, "Lab", null, 1, 0, 3, From, Now));
        }

        [Fact]
        public void Update_changes_a_draft_only()
        {
            var version = NewDraft();

            ResultAssert.Succeeded(version.UpdateDraft("Programming 1", "New", 4, 3, 2, From.AddDays(10), Now.AddDays(1)));

            Assert.Equal("Programming 1", version.Title);
            Assert.Equal(4, version.Credits);
            Assert.Equal(From.AddDays(10), version.EffectiveFrom);
            Assert.Equal(Now.AddDays(1), version.UpdatedAt);
            ResultAssert.Failed(version.UpdateDraft("Programming 1", null, 4, 3, 2, default, Now), CoursesErrors.EffectiveDateInvalid);
            ResultAssert.Failed(version.UpdateDraft("Programming 1", null, 99, 3, 2, From, Now), CoursesErrors.CreditsInvalid);

            ResultAssert.Succeeded(version.Activate(Now));
            ResultAssert.Failed(version.UpdateDraft("Changed", null, 4, 3, 2, From, Now), CoursesErrors.VersionNotDraft);
            Assert.Equal("Programming 1", version.Title);
        }

        [Fact]
        public void A_failed_update_leaves_the_draft_unchanged()
        {
            var version = NewDraft();

            ResultAssert.Failed(version.UpdateDraft("Changed", null, 99, 3, 2, From.AddDays(5), Now), CoursesErrors.CreditsInvalid);

            Assert.Equal("Intro to Programming", version.Title);
            Assert.Equal(3, version.Credits);
            Assert.Equal(From, version.EffectiveFrom);
        }

        [Fact]
        public void Activate_then_retire()
        {
            var version = NewDraft();

            ResultAssert.Succeeded(version.Activate(Now));
            Assert.Equal(CourseVersionStatus.Active, version.Status);
            ResultAssert.Failed(version.Activate(Now), CoursesErrors.VersionInvalidState);

            ResultAssert.Failed(version.Retire(From.AddDays(-1), Now), CoursesErrors.EffectiveToInvalid);
            ResultAssert.Succeeded(version.Retire(From.AddMonths(6), Now));
            Assert.Equal(CourseVersionStatus.Retired, version.Status);
            Assert.Equal(From.AddMonths(6), version.EffectiveTo);
            ResultAssert.Failed(version.Retire(From.AddMonths(7), Now), CoursesErrors.VersionInvalidState);
        }

        [Fact]
        public void A_draft_cannot_be_retired()
        {
            ResultAssert.Failed(NewDraft().Retire(From, Now), CoursesErrors.VersionInvalidState);
        }

        [Fact]
        public void Is_effective_on_follows_status_and_dates()
        {
            var version = NewDraft();
            Assert.False(version.IsEffectiveOn(From));

            ResultAssert.Succeeded(version.Activate(Now));
            Assert.False(version.IsEffectiveOn(From.AddDays(-1)));
            Assert.True(version.IsEffectiveOn(From));
            Assert.True(version.IsEffectiveOn(From.AddYears(5)));

            ResultAssert.Succeeded(version.Retire(From.AddMonths(6), Now));
            Assert.True(version.IsEffectiveOn(From.AddMonths(6)));
            Assert.False(version.IsEffectiveOn(From.AddMonths(6).AddDays(1)));
        }
    }
}
