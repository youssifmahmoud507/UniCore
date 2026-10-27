using UniCore.Application.Modules.Offerings;
using UniCore.Domain.Modules.Offerings;
using UniCore.UnitTests.Common;

namespace UniCore.UnitTests.Modules.Offerings
{
    public class OfferingReadServiceTests
    {
        private static readonly UniCore.Application.Common.Abstractions.RequestInfo Req = OfferingsTestEnv.Request;

        private static async Task<(OfferingsTestEnv Env, Guid VersionId, Guid CourseId, OfferingSummary Offering, SectionSummary Section)> NewWorldAsync()
        {
            var env = new OfferingsTestEnv();
            var version = env.SeedVersion(credits: 4);
            var semester = env.SeedSemester();
            env.AsSuperAdmin();

            var offering = await env.CreateOfferingAsync(version.CourseVersionId, semester.Id, 2);
            var section = await env.CreateSectionAsync(offering.Id, "A", 25);
            return (env, version.CourseVersionId, version.CourseId, offering, section);
        }

        [Fact]
        public async Task Get_offering_returns_the_course_and_whether_it_accepts_enrollment()
        {
            var (env, versionId, courseId, offering, _) = await NewWorldAsync();

            var draft = ResultAssert.Succeeded(await env.ReadService.GetOfferingAsync(offering.Id));
            Assert.Equal(versionId, draft.CourseVersionId);
            Assert.Equal(courseId, draft.CourseId);
            Assert.Equal(2, draft.MaxSections);
            Assert.False(draft.AcceptsEnrollment);

            ResultAssert.Succeeded(await env.OfferingHandler.OpenAsync(offering.Id, Req));
            Assert.True(ResultAssert.Succeeded(await env.ReadService.GetOfferingAsync(offering.Id)).AcceptsEnrollment);

            ResultAssert.Failed(await env.ReadService.GetOfferingAsync(Guid.NewGuid()), OfferingsErrors.OfferingNotFound);
        }

        [Fact]
        public async Task Get_section_returns_credits_capacity_and_open_for_enrollment()
        {
            var (env, _, courseId, offering, section) = await NewWorldAsync();

            var closedOffering = ResultAssert.Succeeded(await env.ReadService.GetSectionAsync(section.Id));
            Assert.Equal(courseId, closedOffering.CourseId);
            Assert.Equal("A", closedOffering.SectionCode);
            Assert.Equal(25, closedOffering.Capacity);
            Assert.Equal(4, closedOffering.Credits);
            Assert.False(closedOffering.IsOpenForEnrollment);

            ResultAssert.Succeeded(await env.OfferingHandler.OpenAsync(offering.Id, Req));
            Assert.True(ResultAssert.Succeeded(await env.ReadService.GetSectionAsync(section.Id)).IsOpenForEnrollment);

            ResultAssert.Succeeded(await env.SectionHandler.CloseAsync(section.Id, Req));
            var closedSection = ResultAssert.Succeeded(await env.ReadService.GetSectionAsync(section.Id));
            Assert.Equal(CourseSectionStatus.Closed, closedSection.SectionStatus);
            Assert.Equal(CourseOfferingStatus.Open, closedSection.OfferingStatus);
            Assert.False(closedSection.IsOpenForEnrollment);

            ResultAssert.Failed(await env.ReadService.GetSectionAsync(Guid.NewGuid()), OfferingsErrors.SectionNotFound);
        }

        [Fact]
        public async Task Active_instructors_leave_out_the_ended_ones()
        {
            var (env, _, _, _, section) = await NewWorldAsync();
            var lead = env.SeedInstructor();
            var assistant = env.SeedInstructor();
            var primary = await env.AssignAsync(section.Id, lead.Id, AssignmentRole.Primary);
            await env.AssignAsync(section.Id, assistant.Id, AssignmentRole.TeachingAssistant);

            Assert.Equal(2, ResultAssert.Succeeded(await env.ReadService.GetActiveInstructorsAsync(section.Id)).Count);

            ResultAssert.Succeeded(await env.AssignmentHandler.EndAsync(section.Id, primary.Id, Req));

            var active = ResultAssert.Succeeded(await env.ReadService.GetActiveInstructorsAsync(section.Id));
            var only = Assert.Single(active);
            Assert.Equal(assistant.Id, only.InstructorId);
            Assert.Equal(AssignmentRole.TeachingAssistant, only.Role);
            ResultAssert.Failed(await env.ReadService.GetActiveInstructorsAsync(Guid.NewGuid()), OfferingsErrors.SectionNotFound);
        }
    }
}
