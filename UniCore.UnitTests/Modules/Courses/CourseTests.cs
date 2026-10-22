using UniCore.Domain.Modules.Courses;
using UniCore.UnitTests.Common;

namespace UniCore.UnitTests.Modules.Courses
{
    public class CourseTests
    {
        private static readonly DateTimeOffset Now = new(2026, 10, 12, 9, 0, 0, TimeSpan.Zero);
        private static readonly Guid DepartmentId = Guid.NewGuid();

        private static Course NewCourse() => ResultAssert.Succeeded(Course.Create(DepartmentId, "cs101", "Intro to Programming", null, CourseType.Mixed, Now));

        [Fact]
        public void Create_normalizes_code_and_starts_active()
        {
            var course = ResultAssert.Succeeded(Course.Create(DepartmentId, " cs101 ", " Intro to Programming ", " First course ", CourseType.Theory, Now));

            Assert.NotEqual(Guid.Empty, course.Id);
            Assert.Equal(DepartmentId, course.DepartmentId);
            Assert.Equal("CS101", course.Code);
            Assert.Equal("Intro to Programming", course.Title);
            Assert.Equal("First course", course.Description);
            Assert.Equal(CourseType.Theory, course.CourseType);
            Assert.True(course.IsActive);
            Assert.Equal(Now, course.CreatedAt);
            Assert.Null(course.UpdatedAt);
        }

        [Fact]
        public void Create_rejects_bad_input()
        {
            ResultAssert.Failed(Course.Create(Guid.Empty, "CS101", "Title", null, CourseType.Theory, Now), CoursesErrors.InvalidParent);
            ResultAssert.Failed(Course.Create(DepartmentId, null, "Title", null, CourseType.Theory, Now), CoursesErrors.InvalidCode);
            ResultAssert.Failed(Course.Create(DepartmentId, "C", "Title", null, CourseType.Theory, Now), CoursesErrors.InvalidCode);
            ResultAssert.Failed(Course.Create(DepartmentId, "CS 101", "Title", null, CourseType.Theory, Now), CoursesErrors.InvalidCode);
            ResultAssert.Failed(Course.Create(DepartmentId, "CS101", " ", null, CourseType.Theory, Now), CoursesErrors.InvalidTitle);
            ResultAssert.Failed(Course.Create(DepartmentId, "CS101", "Title", new string('x', 1001), CourseType.Theory, Now), CoursesErrors.InvalidDescription);
            ResultAssert.Failed(Course.Create(DepartmentId, "CS101", "Title", null, null, Now), CoursesErrors.InvalidCourseType);
            ResultAssert.Failed(Course.Create(DepartmentId, "CS101", "Title", null, (CourseType)99, Now), CoursesErrors.InvalidCourseType);
        }

        [Fact]
        public void Update_changes_details_but_not_the_code()
        {
            var course = NewCourse();

            ResultAssert.Succeeded(course.UpdateDetails("Programming 1", "Notes", CourseType.Practical, Now.AddDays(1)));

            Assert.Equal("CS101", course.Code);
            Assert.Equal("Programming 1", course.Title);
            Assert.Equal(CourseType.Practical, course.CourseType);
            Assert.Equal(Now.AddDays(1), course.UpdatedAt);
            ResultAssert.Failed(course.UpdateDetails(" ", null, CourseType.Practical, Now), CoursesErrors.InvalidTitle);
        }

        [Fact]
        public void Deactivate_is_refused_while_an_active_version_exists()
        {
            var course = NewCourse();

            ResultAssert.Failed(course.Deactivate(hasActiveVersion: true, Now), CoursesErrors.CourseHasActiveVersion);
            Assert.True(course.IsActive);

            ResultAssert.Succeeded(course.Deactivate(hasActiveVersion: false, Now));
            Assert.False(course.IsActive);
            ResultAssert.Failed(course.Deactivate(hasActiveVersion: false, Now), CoursesErrors.CourseAlreadyInactive);

            ResultAssert.Succeeded(course.Activate(Now));
            ResultAssert.Failed(course.Activate(Now), CoursesErrors.CourseAlreadyActive);
        }
    }
}
