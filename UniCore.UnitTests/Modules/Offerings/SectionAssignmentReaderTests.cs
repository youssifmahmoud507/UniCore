using UniCore.Application.Modules.Offerings;
using UniCore.Domain.Modules.Offerings;
using UniCore.UnitTests.Common;
using UniCore.UnitTests.Modules.Admissions;

namespace UniCore.UnitTests.Modules.Offerings
{
    public class SectionAssignmentReaderTests
    {
        private static readonly DateTimeOffset Now = new(2026, 9, 2, 9, 0, 0, TimeSpan.Zero);

        [Fact]
        public async Task Returns_the_sections_an_instructor_teaches_now_in_any_role_and_skips_ended_ones()
        {
            var repository = new FakeAggregateRepository<InstructorAssignment>();
            var instructor = Guid.NewGuid();
            var a = Guid.NewGuid();
            var b = Guid.NewGuid();
            var ended = Guid.NewGuid();

            repository.Items.Add(ResultAssert.Succeeded(InstructorAssignment.Create(a, instructor, AssignmentRole.Primary, Now)));
            repository.Items.Add(ResultAssert.Succeeded(InstructorAssignment.Create(b, instructor, AssignmentRole.CoInstructor, Now)));
            var old = ResultAssert.Succeeded(InstructorAssignment.Create(ended, instructor, AssignmentRole.Primary, Now));
            ResultAssert.Succeeded(old.End(Now.AddDays(1)));
            repository.Items.Add(old);
            repository.Items.Add(ResultAssert.Succeeded(InstructorAssignment.Create(Guid.NewGuid(), Guid.NewGuid(), AssignmentRole.Primary, Now)));

            var reader = new SectionAssignmentReader(repository);

            var sections = await reader.GetAssignedSectionIdsAsync(instructor, null);

            Assert.Equal([a, b], sections.OrderBy(id => id == a ? 0 : 1));
            Assert.Empty(await reader.GetAssignedSectionIdsAsync(null, Guid.NewGuid()));
            Assert.Empty(await reader.GetAssignedSectionIdsAsync(Guid.NewGuid(), null));
        }
    }
}
