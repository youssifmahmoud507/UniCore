using UniCore.Domain.Modules.Offerings;
using UniCore.UnitTests.Common;

namespace UniCore.UnitTests.Modules.Offerings
{
    public class OfferingsDomainTests
    {
        private static readonly DateTimeOffset Now = new(2026, 10, 12, 9, 0, 0, TimeSpan.Zero);
        private static readonly Guid VersionId = Guid.NewGuid();
        private static readonly Guid SemesterId = Guid.NewGuid();

        private static CourseOffering NewOffering(int? max = null) => ResultAssert.Succeeded(CourseOffering.Create(VersionId, SemesterId, max, Now));
        private static CourseSection NewSection(string code = "A", int capacity = 30) => ResultAssert.Succeeded(CourseSection.Create(Guid.NewGuid(), code, capacity, Now));

        // ---------- offering ----------

        [Fact]
        public void Offering_starts_as_a_draft()
        {
            var offering = ResultAssert.Succeeded(CourseOffering.Create(VersionId, SemesterId, 3, Now));

            Assert.NotEqual(Guid.Empty, offering.Id);
            Assert.Equal(VersionId, offering.CourseVersionId);
            Assert.Equal(SemesterId, offering.AcademicSemesterId);
            Assert.Equal(CourseOfferingStatus.Draft, offering.Status);
            Assert.Equal(3, offering.MaxSections);
            Assert.False(offering.AcceptsEnrollment);
        }

        [Fact]
        public void Offering_rejects_bad_input()
        {
            ResultAssert.Failed(CourseOffering.Create(Guid.Empty, SemesterId, null, Now), OfferingsErrors.InvalidParent);
            ResultAssert.Failed(CourseOffering.Create(VersionId, Guid.Empty, null, Now), OfferingsErrors.InvalidParent);
            ResultAssert.Failed(CourseOffering.Create(VersionId, SemesterId, 0, Now), OfferingsErrors.MaxSectionsInvalid);
            ResultAssert.Failed(CourseOffering.Create(VersionId, SemesterId, 51, Now), OfferingsErrors.MaxSectionsInvalid);
        }

        [Fact]
        public void Offering_lifecycle_open_close_reopen_cancel()
        {
            var offering = NewOffering();

            ResultAssert.Failed(offering.Open(0, Now), OfferingsErrors.OfferingNeedsSection);
            ResultAssert.Failed(offering.Close(Now), OfferingsErrors.OfferingInvalidState);

            ResultAssert.Succeeded(offering.Open(1, Now));
            Assert.True(offering.AcceptsEnrollment);
            ResultAssert.Failed(offering.Open(1, Now), OfferingsErrors.OfferingInvalidState);

            ResultAssert.Succeeded(offering.Close(Now));
            Assert.False(offering.AcceptsEnrollment);

            ResultAssert.Succeeded(offering.Open(1, Now));
            ResultAssert.Succeeded(offering.Cancel(Now));
            Assert.Equal(CourseOfferingStatus.Cancelled, offering.Status);

            ResultAssert.Failed(offering.Cancel(Now), OfferingsErrors.OfferingInvalidState);
            ResultAssert.Failed(offering.Open(1, Now), OfferingsErrors.OfferingInvalidState);
            ResultAssert.Failed(offering.Close(Now), OfferingsErrors.OfferingInvalidState);
        }

        [Fact]
        public void Max_sections_cannot_go_below_the_sections_in_use()
        {
            var offering = NewOffering(5);

            ResultAssert.Failed(offering.ChangeMaxSections(1, 2, Now), OfferingsErrors.MaxSectionsInvalid);
            ResultAssert.Failed(offering.ChangeMaxSections(0, 0, Now), OfferingsErrors.MaxSectionsInvalid);
            ResultAssert.Succeeded(offering.ChangeMaxSections(2, 2, Now));
            Assert.Equal(2, offering.MaxSections);
            ResultAssert.Succeeded(offering.ChangeMaxSections(null, 2, Now));
            Assert.Null(offering.MaxSections);

            ResultAssert.Succeeded(offering.Cancel(Now));
            ResultAssert.Failed(offering.ChangeMaxSections(3, 0, Now), OfferingsErrors.OfferingInvalidState);
        }

        // ---------- section ----------

        [Fact]
        public void Section_normalizes_the_code_and_starts_open()
        {
            var section = ResultAssert.Succeeded(CourseSection.Create(Guid.NewGuid(), " a1 ", 40, Now));

            Assert.Equal("A1", section.SectionCode);
            Assert.Equal(40, section.Capacity);
            Assert.Equal(CourseSectionStatus.Open, section.Status);
            Assert.True(section.IsOpen);
        }

        [Fact]
        public void Section_rejects_bad_input()
        {
            ResultAssert.Failed(CourseSection.Create(Guid.Empty, "A", 30, Now), OfferingsErrors.InvalidParent);
            ResultAssert.Failed(CourseSection.Create(Guid.NewGuid(), null, 30, Now), OfferingsErrors.SectionCodeInvalid);
            ResultAssert.Failed(CourseSection.Create(Guid.NewGuid(), "A B", 30, Now), OfferingsErrors.SectionCodeInvalid);
            ResultAssert.Failed(CourseSection.Create(Guid.NewGuid(), "ABCDEFGHIJK", 30, Now), OfferingsErrors.SectionCodeInvalid);
            ResultAssert.Failed(CourseSection.Create(Guid.NewGuid(), "A", null, Now), OfferingsErrors.CapacityInvalid);
            ResultAssert.Failed(CourseSection.Create(Guid.NewGuid(), "A", 0, Now), OfferingsErrors.CapacityInvalid);
            ResultAssert.Failed(CourseSection.Create(Guid.NewGuid(), "A", 1001, Now), OfferingsErrors.CapacityInvalid);
        }

        [Fact]
        public void Capacity_cannot_go_below_the_enrolled_count()
        {
            var section = NewSection(capacity: 30);

            ResultAssert.Failed(section.ChangeCapacity(10, 15, Now), OfferingsErrors.CapacityBelowEnrolled);
            ResultAssert.Failed(section.ChangeCapacity(null, 0, Now), OfferingsErrors.CapacityInvalid);
            ResultAssert.Failed(section.ChangeCapacity(0, 0, Now), OfferingsErrors.CapacityInvalid);
            Assert.Equal(30, section.Capacity);

            ResultAssert.Succeeded(section.ChangeCapacity(15, 15, Now));
            Assert.Equal(15, section.Capacity);
        }

        [Fact]
        public void Section_lifecycle_close_reopen_cancel()
        {
            var section = NewSection();

            ResultAssert.Failed(section.Reopen(Now), OfferingsErrors.SectionInvalidState);
            ResultAssert.Succeeded(section.Close(Now));
            Assert.False(section.IsOpen);
            ResultAssert.Failed(section.Close(Now), OfferingsErrors.SectionInvalidState);
            ResultAssert.Succeeded(section.Reopen(Now));

            ResultAssert.Failed(section.Cancel(2, Now), OfferingsErrors.SectionHasEnrollments);
            Assert.Equal(CourseSectionStatus.Open, section.Status);

            ResultAssert.Succeeded(section.Cancel(0, Now));
            Assert.Equal(CourseSectionStatus.Cancelled, section.Status);
            ResultAssert.Failed(section.Cancel(0, Now), OfferingsErrors.SectionInvalidState);
            ResultAssert.Failed(section.Reopen(Now), OfferingsErrors.SectionInvalidState);
            ResultAssert.Failed(section.ChangeCapacity(10, 0, Now), OfferingsErrors.SectionInvalidState);
        }

        // ---------- instructor assignment ----------

        [Fact]
        public void Assignment_starts_active_and_ending_it_keeps_the_row()
        {
            var assignment = ResultAssert.Succeeded(InstructorAssignment.Create(Guid.NewGuid(), Guid.NewGuid(), AssignmentRole.Primary, Now));

            Assert.True(assignment.IsActive);
            Assert.Equal(Now, assignment.AssignedAt);
            Assert.Null(assignment.EndedAt);

            ResultAssert.Succeeded(assignment.End(Now.AddDays(30)));
            Assert.False(assignment.IsActive);
            Assert.Equal(Now.AddDays(30), assignment.EndedAt);

            ResultAssert.Failed(assignment.End(Now.AddDays(31)), OfferingsErrors.AssignmentAlreadyEnded);
            Assert.Equal(Now.AddDays(30), assignment.EndedAt);
        }

        [Fact]
        public void Assignment_never_ends_before_it_starts()
        {
            var assignment = ResultAssert.Succeeded(InstructorAssignment.Create(Guid.NewGuid(), Guid.NewGuid(), AssignmentRole.TeachingAssistant, Now));

            ResultAssert.Succeeded(assignment.End(Now.AddDays(-1)));

            Assert.Equal(Now, assignment.EndedAt);
        }

        [Fact]
        public void Assignment_rejects_bad_input()
        {
            ResultAssert.Failed(InstructorAssignment.Create(Guid.Empty, Guid.NewGuid(), AssignmentRole.Primary, Now), OfferingsErrors.AssignmentInvalid);
            ResultAssert.Failed(InstructorAssignment.Create(Guid.NewGuid(), Guid.Empty, AssignmentRole.Primary, Now), OfferingsErrors.AssignmentInvalid);
            ResultAssert.Failed(InstructorAssignment.Create(Guid.NewGuid(), Guid.NewGuid(), null, Now), OfferingsErrors.AssignmentInvalid);
            ResultAssert.Failed(InstructorAssignment.Create(Guid.NewGuid(), Guid.NewGuid(), (AssignmentRole)99, Now), OfferingsErrors.AssignmentInvalid);
        }
    }
}
