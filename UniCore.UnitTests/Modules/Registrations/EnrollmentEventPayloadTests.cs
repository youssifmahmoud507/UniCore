using UniCore.Domain.Modules.Registrations;
using UniCore.UnitTests.Common;

namespace UniCore.UnitTests.Modules.Registrations
{
    // The enrollment events are a contract with Finance (charges are generated and reversed from them), so the fields it
    // needs are pinned here: student, section, course, credits and semester. Removing or renaming one fails these tests.
    public class EnrollmentEventPayloadTests
    {
        private static readonly DateTimeOffset Now = new(2026, 9, 2, 9, 0, 0, TimeSpan.Zero);

        private static (Registration Registration, Guid Student, Guid Semester) NewRegistration()
        {
            var student = Guid.NewGuid();
            var semester = Guid.NewGuid();

            return (ResultAssert.Succeeded(Registration.Create(student, semester, Now)), student, semester);
        }

        [Fact]
        public void EnrollmentCreated_carries_student_section_course_credits_and_semester()
        {
            var (registration, student, semester) = NewRegistration();
            var section = Guid.NewGuid();
            var course = Guid.NewGuid();

            var enrollment = ResultAssert.Succeeded(registration.Enroll(section, course, Guid.NewGuid(), 4, null, Now));

            var created = Assert.Single(registration.DomainEvents.OfType<EnrollmentCreated>());
            Assert.Equal(enrollment.Id, created.EnrollmentId);
            Assert.Equal(registration.Id, created.RegistrationId);
            Assert.Equal(student, created.StudentId);
            Assert.Equal(section, created.CourseSectionId);
            Assert.Equal(course, created.CourseId);
            Assert.Equal(semester, created.AcademicSemesterId);
            Assert.Equal(4, created.Credits);
            Assert.NotEqual(Guid.Empty, created.EventId);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void EnrollmentDropped_carries_the_same_fields_for_a_drop_and_a_withdrawal(bool withdraw)
        {
            var (registration, student, semester) = NewRegistration();
            var section = Guid.NewGuid();
            var course = Guid.NewGuid();
            var enrollment = ResultAssert.Succeeded(registration.Enroll(section, course, Guid.NewGuid(), 3, null, Now));

            ResultAssert.Succeeded(withdraw ? registration.Withdraw(enrollment.Id, Now) : registration.Drop(enrollment.Id, Now));

            var dropped = Assert.Single(registration.DomainEvents.OfType<EnrollmentDropped>());
            Assert.Equal(enrollment.Id, dropped.EnrollmentId);
            Assert.Equal(student, dropped.StudentId);
            Assert.Equal(section, dropped.CourseSectionId);
            Assert.Equal(course, dropped.CourseId);
            Assert.Equal(semester, dropped.AcademicSemesterId);
            Assert.Equal(3, dropped.Credits);
            Assert.Equal(withdraw, dropped.IsWithdrawal);
        }

        [Fact]
        public void Cancelling_a_registration_raises_one_EnrollmentDropped_per_seat_with_the_course()
        {
            var (registration, student, _) = NewRegistration();
            var courses = new[] { Guid.NewGuid(), Guid.NewGuid() };
            foreach (var course in courses)
            {
                ResultAssert.Succeeded(registration.Enroll(Guid.NewGuid(), course, Guid.NewGuid(), 3, null, Now));
            }

            ResultAssert.Succeeded(registration.Cancel(Now));

            var dropped = registration.DomainEvents.OfType<EnrollmentDropped>().ToList();
            Assert.Equal(2, dropped.Count);
            Assert.All(dropped, d =>
            {
                Assert.Equal(student, d.StudentId);
                Assert.False(d.IsWithdrawal);
                Assert.Equal(3, d.Credits);
            });
            Assert.Equal(courses.Order(), dropped.Select(d => d.CourseId).Order());
        }
    }
}
