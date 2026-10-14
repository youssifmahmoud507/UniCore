using UniCore.Domain.Common.Entities;
using UniCore.Domain.Modules.Students;

namespace UniCore.UnitTests.Modules.Students
{
    public class StudentIdentityTests
    {
        private static readonly DateTimeOffset Now = new(2026, 10, 11, 12, 0, 0, TimeSpan.Zero);
        private static readonly DateOnly Admission = new(2026, 9, 1);

        private static Student NewStudent(string number = "2026CS0001")
        {
            var result = Student.Create(Guid.NewGuid(), number, Admission, null, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Now);

            Assert.True(result.TryGetValue(out var student));
            Assert.NotNull(student);
            return student;
        }

        [Fact]
        public void Student_is_an_aggregate_root_with_an_identity_and_one_admission_event()
        {
            var student = NewStudent();

            Assert.IsAssignableFrom<AggregateRoot>(student);
            Assert.NotEqual(Guid.Empty, student.Id);
            Assert.IsType<StudentAdmitted>(Assert.Single(student.DomainEvents));
        }

        [Fact]
        public void The_admission_event_describes_the_new_student()
        {
            var student = NewStudent("2026CS0007");

            var admitted = Assert.IsType<StudentAdmitted>(Assert.Single(student.DomainEvents));
            Assert.NotEqual(Guid.Empty, admitted.EventId);
            Assert.Equal(Now, admitted.OccurredAt);
            Assert.Equal(student.Id, admitted.StudentId);
            Assert.Equal(student.PersonProfileId, admitted.PersonProfileId);
            Assert.Equal("2026CS0007", admitted.StudentNumber);
            Assert.Equal(student.CurrentProgram?.AcademicProgramId, admitted.AcademicProgramId);
            Assert.Equal(Admission, admitted.AdmissionDate);
        }

        [Fact]
        public void A_rejected_student_raises_no_event_and_clearing_events_empties_the_list()
        {
            var rejected = Student.Create(Guid.Empty, "2026CS0001", Admission, null, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null, Now);
            Assert.True(rejected.IsFailure);

            var student = NewStudent();
            student.ClearDomainEvents();

            Assert.Empty(student.DomainEvents);
        }

        [Fact]
        public void Students_are_equal_only_by_identity()
        {
            var first = NewStudent("2026CS0001");
            var second = NewStudent("2026CS0002");

            Assert.True(first.Equals(first));
            Assert.False(first.Equals(second));
            Assert.True(first != second);
        }

        [Fact]
        public void Children_are_base_entities_that_point_to_their_student()
        {
            var student = NewStudent();

            var profile = student.Profile;
            var program = student.CurrentProgram;

            Assert.NotNull(profile);
            Assert.NotNull(program);
            Assert.IsAssignableFrom<BaseEntity>(profile);
            Assert.IsAssignableFrom<BaseEntity>(program);
            Assert.NotEqual(Guid.Empty, profile.Id);
            Assert.Equal(student.Id, profile.StudentId);
            Assert.Equal(student.Id, program.StudentId);
        }
    }

}
