using UniCore.Application.Common.Pagination;
using UniCore.Application.Modules.Offerings;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Calendar;
using UniCore.Domain.Modules.Courses;
using UniCore.Domain.Modules.Identity;
using UniCore.Domain.Modules.Offerings;
using UniCore.UnitTests.Common;
using UniCore.UnitTests.Modules.Identity;

namespace UniCore.UnitTests.Modules.Offerings
{
    public class OfferingsHandlersTests
    {
        private static readonly PaginationQuery FirstPage = new(1, 20);
        private static readonly UniCore.Application.Common.Abstractions.RequestInfo Req = OfferingsTestEnv.Request;

        private static (OfferingsTestEnv Env, Guid VersionId, Guid SemesterId) NewWorld()
        {
            var env = new OfferingsTestEnv();
            var version = env.SeedVersion();
            var semester = env.SeedSemester();
            env.AsSuperAdmin();
            return (env, version.CourseVersionId, semester.Id);
        }

        // ---------- offerings ----------

        [Fact]
        public async Task Offering_create_succeeds_saves_once_and_audits()
        {
            var (env, versionId, semesterId) = NewWorld();

            var summary = ResultAssert.Succeeded(await env.OfferingHandler.CreateAsync(new CreateOfferingCommand(versionId, semesterId, 2), Req));

            Assert.Equal(CourseOfferingStatus.Draft, summary.Status);
            Assert.Equal(2, summary.MaxSections);
            Assert.Equal(1, env.Rbac.Uow.SaveCalls);

            var audit = Assert.Single(env.Rbac.Audit.Entries);
            Assert.Equal(OfferingsAuditActions.OfferingCreated, audit.Action);
            Assert.Equal(env.ActorId, audit.ActorUserId);
            Assert.Equal(summary.Id.ToString(), audit.EntityId);
        }

        [Fact]
        public async Task Offering_create_checks_the_version_the_semester_and_duplicates()
        {
            var (env, versionId, semesterId) = NewWorld();
            var draftVersion = env.SeedVersion("CS102", CourseVersionStatus.Draft);
            var futureVersion = env.SeedVersion("CS103", from: new DateOnly(2027, 1, 1));
            var endedVersion = env.SeedVersion("CS104", CourseVersionStatus.Retired, to: new DateOnly(2026, 8, 31));
            var closedSemester = env.SeedSemester(closed: true);

            ResultAssert.Failed(await env.OfferingHandler.CreateAsync(new CreateOfferingCommand(Guid.NewGuid(), semesterId, null), Req), OfferingsErrors.CourseVersionNotFound);
            ResultAssert.Failed(await env.OfferingHandler.CreateAsync(new CreateOfferingCommand(versionId, Guid.NewGuid(), null), Req), OfferingsErrors.SemesterNotFound);
            ResultAssert.Failed(await env.OfferingHandler.CreateAsync(new CreateOfferingCommand(versionId, closedSemester.Id, null), Req), OfferingsErrors.SemesterClosed);
            ResultAssert.Failed(await env.OfferingHandler.CreateAsync(new CreateOfferingCommand(draftVersion.CourseVersionId, semesterId, null), Req), OfferingsErrors.CourseVersionNotEffective);
            ResultAssert.Failed(await env.OfferingHandler.CreateAsync(new CreateOfferingCommand(futureVersion.CourseVersionId, semesterId, null), Req), OfferingsErrors.CourseVersionNotEffective);
            ResultAssert.Failed(await env.OfferingHandler.CreateAsync(new CreateOfferingCommand(endedVersion.CourseVersionId, semesterId, null), Req), OfferingsErrors.CourseVersionNotEffective);
            ResultAssert.Failed(await env.OfferingHandler.CreateAsync(new CreateOfferingCommand(versionId, semesterId, 99), Req), OfferingsErrors.MaxSectionsInvalid);
            Assert.Empty(env.Rbac.Audit.Entries);

            await env.CreateOfferingAsync(versionId, semesterId);
            ResultAssert.Failed(await env.OfferingHandler.CreateAsync(new CreateOfferingCommand(versionId, semesterId, null), Req), OfferingsErrors.OfferingExists);
        }

        [Fact]
        public async Task A_version_that_was_retired_after_the_semester_started_can_still_be_offered()
        {
            var (env, _, semesterId) = NewWorld();
            var retiredLater = env.SeedVersion("CS105", CourseVersionStatus.Retired, to: new DateOnly(2027, 8, 31));

            ResultAssert.Succeeded(await env.OfferingHandler.CreateAsync(new CreateOfferingCommand(retiredLater.CourseVersionId, semesterId, null), Req));
        }

        [Fact]
        public async Task Offering_changes_need_the_permission_and_the_courses_department_scope()
        {
            var env = new OfferingsTestEnv();
            var version = env.SeedVersion();
            var semester = env.SeedSemester();
            var facultyA = Guid.NewGuid();
            var facultyB = Guid.NewGuid();
            env.PlaceDepartmentInFaculty(env.DepartmentId, facultyB);

            env.AsDeanOf(facultyB);
            var offering = await env.CreateOfferingAsync(version.CourseVersionId, semester.Id);

            env.AsReader();
            ResultAssert.Failed(await env.OfferingHandler.CreateAsync(new CreateOfferingCommand(version.CourseVersionId, semester.Id, null), Req), IdentityErrors.ForbiddenPermissionMissing);
            ResultAssert.Failed(await env.OfferingHandler.OpenAsync(offering.Id, Req), IdentityErrors.ForbiddenPermissionMissing);

            env.AsDeanOf(facultyA);
            ResultAssert.Failed(await env.OfferingHandler.UpdateAsync(offering.Id, new UpdateOfferingCommand(3), Req), IdentityErrors.ForbiddenOutOfScope);
            ResultAssert.Failed(await env.OfferingHandler.CreateAsync(new CreateOfferingCommand(version.CourseVersionId, semester.Id, null), Req), IdentityErrors.ForbiddenOutOfScope);

            env.AsDeanOf(facultyB);
            ResultAssert.Succeeded(await env.OfferingHandler.UpdateAsync(offering.Id, new UpdateOfferingCommand(3), Req));
        }

        [Fact]
        public async Task Without_the_permission_an_unknown_offering_looks_the_same_as_a_real_one()
        {
            var (env, versionId, semesterId) = NewWorld();
            var offering = await env.CreateOfferingAsync(versionId, semesterId);
            env.AsReader();

            ResultAssert.Failed(await env.OfferingHandler.CancelAsync(offering.Id, Req), IdentityErrors.ForbiddenPermissionMissing);
            ResultAssert.Failed(await env.OfferingHandler.CancelAsync(Guid.NewGuid(), Req), IdentityErrors.ForbiddenPermissionMissing);
        }

        [Fact]
        public async Task Offering_open_needs_a_section_then_close_and_reopen()
        {
            var (env, versionId, semesterId) = NewWorld();
            var offering = await env.CreateOfferingAsync(versionId, semesterId);

            ResultAssert.Failed(await env.OfferingHandler.OpenAsync(offering.Id, Req), OfferingsErrors.OfferingNeedsSection);

            await env.CreateSectionAsync(offering.Id);
            var opened = ResultAssert.Succeeded(await env.OfferingHandler.OpenAsync(offering.Id, Req));
            Assert.Equal(CourseOfferingStatus.Open, opened.Status);
            Assert.Equal(OfferingsAuditActions.OfferingOpened, env.Rbac.Audit.Entries.Last().Action);

            ResultAssert.Failed(await env.OfferingHandler.OpenAsync(offering.Id, Req), OfferingsErrors.OfferingInvalidState);

            var closed = ResultAssert.Succeeded(await env.OfferingHandler.CloseAsync(offering.Id, Req));
            Assert.Equal(CourseOfferingStatus.Closed, closed.Status);

            ResultAssert.Succeeded(await env.OfferingHandler.OpenAsync(offering.Id, Req));
        }

        [Fact]
        public async Task A_closed_semester_freezes_its_offerings()
        {
            var (env, versionId, _) = NewWorld();
            var semester = env.SeedSemester();
            var offering = await env.CreateOfferingAsync(versionId, semester.Id);
            await env.CreateSectionAsync(offering.Id);

            ResultAssert.Succeeded(env.Semesters.Items.Single(s => s.Id == semester.Id).AdvanceTo(SemesterStatus.RegistrationOpen, env.Clock.UtcNow));
            foreach (var status in new[] { SemesterStatus.InProgress, SemesterStatus.Examinations, SemesterStatus.GradingOpen, SemesterStatus.Closed })
            {
                ResultAssert.Succeeded(env.Semesters.Items.Single(s => s.Id == semester.Id).AdvanceTo(status, env.Clock.UtcNow));
            }

            ResultAssert.Failed(await env.OfferingHandler.OpenAsync(offering.Id, Req), OfferingsErrors.SemesterClosed);
            ResultAssert.Failed(await env.OfferingHandler.UpdateAsync(offering.Id, new UpdateOfferingCommand(2), Req), OfferingsErrors.SemesterClosed);
            ResultAssert.Failed(await env.OfferingHandler.CancelAsync(offering.Id, Req), OfferingsErrors.SemesterClosed);
            ResultAssert.Failed(await env.SectionHandler.CreateAsync(offering.Id, new CreateSectionCommand("B", 20), Req), OfferingsErrors.SemesterClosed);
        }

        [Fact]
        public async Task Offering_cancel_cancels_sections_and_ends_assignments_in_one_save()
        {
            var (env, versionId, semesterId) = NewWorld();
            var offering = await env.CreateOfferingAsync(versionId, semesterId);
            var sectionA = await env.CreateSectionAsync(offering.Id, "A");
            var sectionB = await env.CreateSectionAsync(offering.Id, "B");
            var instructor = env.SeedInstructor();
            await env.AssignAsync(sectionA.Id, instructor.Id, AssignmentRole.Primary);

            var saves = env.Rbac.Uow.SaveCalls;
            var cancelled = ResultAssert.Succeeded(await env.OfferingHandler.CancelAsync(offering.Id, Req));

            Assert.Equal(CourseOfferingStatus.Cancelled, cancelled.Status);
            Assert.Equal(saves + 1, env.Rbac.Uow.SaveCalls);
            Assert.All(env.Sections.Items, s => Assert.Equal(CourseSectionStatus.Cancelled, s.Status));
            Assert.All(env.Assignments.Items, a => Assert.False(a.IsActive));
            Assert.Contains("SectionsCancelled=2", env.Rbac.Audit.Entries.Last().NewValues);
            Assert.NotEqual(Guid.Empty, sectionB.Id);

            ResultAssert.Failed(await env.OfferingHandler.CancelAsync(offering.Id, Req), OfferingsErrors.OfferingInvalidState);
        }

        [Fact]
        public async Task Offering_cancel_is_refused_while_students_are_enrolled()
        {
            var (env, versionId, semesterId) = NewWorld();
            var (offering, section) = await env.OpenOfferingAsync(versionId, semesterId);
            env.Enrollments.Counts[section.Id] = 4;

            ResultAssert.Failed(await env.OfferingHandler.CancelAsync(offering.Id, Req), OfferingsErrors.SectionHasEnrollments);

            Assert.Equal(CourseOfferingStatus.Open, env.Offerings.Items.Single().Status);
            Assert.Equal(CourseSectionStatus.Open, env.Sections.Items.Single().Status);
        }

        [Fact]
        public async Task Offering_update_keeps_the_limit_above_the_sections_in_use()
        {
            var (env, versionId, semesterId) = NewWorld();
            var offering = await env.CreateOfferingAsync(versionId, semesterId, 3);
            await env.CreateSectionAsync(offering.Id, "A");
            await env.CreateSectionAsync(offering.Id, "B");

            ResultAssert.Failed(await env.OfferingHandler.UpdateAsync(offering.Id, new UpdateOfferingCommand(1), Req), OfferingsErrors.MaxSectionsInvalid);
            var updated = ResultAssert.Succeeded(await env.OfferingHandler.UpdateAsync(offering.Id, new UpdateOfferingCommand(2), Req));
            Assert.Equal(2, updated.MaxSections);
            Assert.Equal(OfferingsAuditActions.OfferingUpdated, env.Rbac.Audit.Entries.Last().Action);
        }

        [Fact]
        public async Task Offering_list_get_and_sort_validation()
        {
            var (env, versionId, semesterId) = NewWorld();
            var other = env.SeedVersion("CS200");
            var offering = await env.CreateOfferingAsync(versionId, semesterId);
            await env.CreateOfferingAsync(other.CourseVersionId, semesterId);

            var page = ResultAssert.Succeeded(await env.OfferingHandler.ListAsync(FirstPage, "-status", semesterId, null, null));
            Assert.Equal(2, page.TotalCount);
            Assert.Equal(1, ResultAssert.Succeeded(await env.OfferingHandler.ListAsync(FirstPage, null, null, versionId, null)).TotalCount);
            Assert.Equal(0, ResultAssert.Succeeded(await env.OfferingHandler.ListAsync(FirstPage, null, null, null, CourseOfferingStatus.Open)).TotalCount);
            Assert.Equal(offering.Id, ResultAssert.Succeeded(await env.OfferingHandler.GetAsync(offering.Id)).Id);
            ResultAssert.Failed(await env.OfferingHandler.GetAsync(Guid.NewGuid()), OfferingsErrors.OfferingNotFound);
            Assert.True((await env.OfferingHandler.ListAsync(FirstPage, "code", null, null, null)).IsFailure);

            env.Rbac.SignIn(Guid.NewGuid(), TestGrants.Role(RoleNames.Student, []));
            ResultAssert.Failed(await env.OfferingHandler.ListAsync(FirstPage, null, null, null, null), IdentityErrors.ForbiddenPermissionMissing);
        }

        // ---------- sections ----------

        [Fact]
        public async Task Section_create_normalizes_the_code_and_audits()
        {
            var (env, versionId, semesterId) = NewWorld();
            var offering = await env.CreateOfferingAsync(versionId, semesterId);

            var section = ResultAssert.Succeeded(await env.SectionHandler.CreateAsync(offering.Id, new CreateSectionCommand("a1", 25), Req));

            Assert.Equal("A1", section.SectionCode);
            Assert.Equal(25, section.Capacity);
            Assert.Equal(CourseSectionStatus.Open, section.Status);
            Assert.Equal(OfferingsAuditActions.SectionCreated, env.Rbac.Audit.Entries.Last().Action);
        }

        [Fact]
        public async Task Section_create_rejects_a_duplicate_code_the_limit_and_a_cancelled_offering()
        {
            var (env, versionId, semesterId) = NewWorld();
            var offering = await env.CreateOfferingAsync(versionId, semesterId, 2);
            await env.CreateSectionAsync(offering.Id, "A");
            await env.CreateSectionAsync(offering.Id, "B");

            ResultAssert.Failed(await env.SectionHandler.CreateAsync(offering.Id, new CreateSectionCommand("a", 20), Req), OfferingsErrors.SectionCodeExists);
            ResultAssert.Failed(await env.SectionHandler.CreateAsync(offering.Id, new CreateSectionCommand("C", 20), Req), OfferingsErrors.MaxSectionsReached);
            ResultAssert.Failed(await env.SectionHandler.CreateAsync(offering.Id, new CreateSectionCommand("C", 0), Req), OfferingsErrors.CapacityInvalid);
            ResultAssert.Failed(await env.SectionHandler.CreateAsync(Guid.NewGuid(), new CreateSectionCommand("C", 20), Req), OfferingsErrors.OfferingNotFound);

            // A cancelled section frees a place under the limit.
            var cancelled = env.Sections.Items.Single(s => s.SectionCode == "B");
            ResultAssert.Succeeded(cancelled.Cancel(0, env.Clock.UtcNow));
            ResultAssert.Succeeded(await env.SectionHandler.CreateAsync(offering.Id, new CreateSectionCommand("C", 20), Req));

            ResultAssert.Succeeded(await env.OfferingHandler.CancelAsync(offering.Id, Req));
            ResultAssert.Failed(await env.SectionHandler.CreateAsync(offering.Id, new CreateSectionCommand("D", 20), Req), OfferingsErrors.OfferingInvalidState);
        }

        [Fact]
        public async Task Section_capacity_cannot_go_below_the_enrolled_count()
        {
            var (env, versionId, semesterId) = NewWorld();
            var offering = await env.CreateOfferingAsync(versionId, semesterId);
            var section = await env.CreateSectionAsync(offering.Id, capacity: 30);
            env.Enrollments.Counts[section.Id] = 12;

            ResultAssert.Failed(await env.SectionHandler.UpdateAsync(section.Id, new UpdateSectionCommand(10), Req), OfferingsErrors.CapacityBelowEnrolled);
            ResultAssert.Failed(await env.SectionHandler.UpdateAsync(section.Id, new UpdateSectionCommand(null), Req), OfferingsErrors.CapacityInvalid);

            var updated = ResultAssert.Succeeded(await env.SectionHandler.UpdateAsync(section.Id, new UpdateSectionCommand(12), Req));
            Assert.Equal(12, updated.Capacity);
        }

        [Fact]
        public async Task Section_close_reopen_and_cancel()
        {
            var (env, versionId, semesterId) = NewWorld();
            var offering = await env.CreateOfferingAsync(versionId, semesterId);
            var section = await env.CreateSectionAsync(offering.Id);

            Assert.Equal(CourseSectionStatus.Closed, ResultAssert.Succeeded(await env.SectionHandler.CloseAsync(section.Id, Req)).Status);
            ResultAssert.Failed(await env.SectionHandler.CloseAsync(section.Id, Req), OfferingsErrors.SectionInvalidState);
            Assert.Equal(CourseSectionStatus.Open, ResultAssert.Succeeded(await env.SectionHandler.ReopenAsync(section.Id, Req)).Status);

            env.Enrollments.Counts[section.Id] = 1;
            ResultAssert.Failed(await env.SectionHandler.CancelAsync(section.Id, Req), OfferingsErrors.SectionHasEnrollments);

            env.Enrollments.Counts[section.Id] = 0;
            Assert.Equal(CourseSectionStatus.Cancelled, ResultAssert.Succeeded(await env.SectionHandler.CancelAsync(section.Id, Req)).Status);
            Assert.Equal(OfferingsAuditActions.SectionCancelled, env.Rbac.Audit.Entries.Last().Action);
            ResultAssert.Failed(await env.SectionHandler.ReopenAsync(section.Id, Req), OfferingsErrors.SectionInvalidState);
        }

        [Fact]
        public async Task Section_cancel_ends_its_active_assignments()
        {
            var (env, versionId, semesterId) = NewWorld();
            var offering = await env.CreateOfferingAsync(versionId, semesterId);
            var section = await env.CreateSectionAsync(offering.Id);
            var first = env.SeedInstructor();
            var second = env.SeedInstructor();
            await env.AssignAsync(section.Id, first.Id, AssignmentRole.Primary);
            await env.AssignAsync(section.Id, second.Id, AssignmentRole.TeachingAssistant);

            ResultAssert.Succeeded(await env.SectionHandler.CancelAsync(section.Id, Req));

            Assert.All(env.Assignments.Items, a => Assert.False(a.IsActive));
            Assert.Contains("AssignmentsEnded=2", env.Rbac.Audit.Entries.Last().NewValues);
        }

        [Fact]
        public async Task Section_changes_are_limited_to_the_deans_faculty_and_reads_need_courses_read()
        {
            var env = new OfferingsTestEnv();
            var version = env.SeedVersion();
            var semester = env.SeedSemester();
            var home = Guid.NewGuid();
            env.PlaceDepartmentInFaculty(env.DepartmentId, home);

            env.AsDeanOf(home);
            var offering = await env.CreateOfferingAsync(version.CourseVersionId, semester.Id);
            var section = await env.CreateSectionAsync(offering.Id);

            env.AsDeanOf(Guid.NewGuid());
            ResultAssert.Failed(await env.SectionHandler.CloseAsync(section.Id, Req), IdentityErrors.ForbiddenOutOfScope);
            ResultAssert.Failed(await env.SectionHandler.CreateAsync(offering.Id, new CreateSectionCommand("B", 20), Req), IdentityErrors.ForbiddenOutOfScope);

            env.AsReader();
            ResultAssert.Failed(await env.SectionHandler.CloseAsync(section.Id, Req), IdentityErrors.ForbiddenPermissionMissing);
            ResultAssert.Succeeded(await env.SectionHandler.GetAsync(section.Id));
            Assert.Single(ResultAssert.Succeeded(await env.SectionHandler.ListAsync(offering.Id)));

            env.Rbac.SignIn(Guid.NewGuid(), TestGrants.Role(RoleNames.Student, []));
            ResultAssert.Failed(await env.SectionHandler.GetAsync(section.Id), IdentityErrors.ForbiddenPermissionMissing);
        }

        [Fact]
        public async Task Section_get_and_list_report_unknown_ids()
        {
            var (env, _, _) = NewWorld();

            ResultAssert.Failed(await env.SectionHandler.GetAsync(Guid.NewGuid()), OfferingsErrors.SectionNotFound);
            ResultAssert.Failed(await env.SectionHandler.ListAsync(Guid.NewGuid()), OfferingsErrors.OfferingNotFound);
        }

        // ---------- instructor assignments ----------

        [Fact]
        public async Task Assign_instructor_succeeds_and_audits()
        {
            var (env, versionId, semesterId) = NewWorld();
            var offering = await env.CreateOfferingAsync(versionId, semesterId);
            var section = await env.CreateSectionAsync(offering.Id);
            var instructor = env.SeedInstructor();

            var assignment = ResultAssert.Succeeded(await env.AssignmentHandler.AssignAsync(section.Id, new AssignInstructorCommand(instructor.Id, AssignmentRole.Primary), Req));

            Assert.Equal(instructor.Id, assignment.InstructorId);
            Assert.Equal(AssignmentRole.Primary, assignment.Role);
            Assert.Null(assignment.EndedAt);
            Assert.Equal(OfferingsAuditActions.InstructorAssigned, env.Rbac.Audit.Entries.Last().Action);
        }

        [Fact]
        public async Task Assign_instructor_rejects_bad_references()
        {
            var (env, versionId, semesterId) = NewWorld();
            var offering = await env.CreateOfferingAsync(versionId, semesterId);
            var section = await env.CreateSectionAsync(offering.Id);
            var inactive = env.SeedInstructor(active: false);
            var audits = env.Rbac.Audit.Entries.Count;

            ResultAssert.Failed(await env.AssignmentHandler.AssignAsync(Guid.NewGuid(), new AssignInstructorCommand(Guid.NewGuid(), AssignmentRole.Primary), Req), OfferingsErrors.SectionNotFound);
            ResultAssert.Failed(await env.AssignmentHandler.AssignAsync(section.Id, new AssignInstructorCommand(Guid.NewGuid(), null), Req), OfferingsErrors.AssignmentInvalid);
            ResultAssert.Failed(await env.AssignmentHandler.AssignAsync(section.Id, new AssignInstructorCommand(Guid.Empty, AssignmentRole.Primary), Req), OfferingsErrors.AssignmentInvalid);
            ResultAssert.Failed(await env.AssignmentHandler.AssignAsync(section.Id, new AssignInstructorCommand(Guid.NewGuid(), AssignmentRole.Primary), Req), OfferingsErrors.InstructorNotFound);
            ResultAssert.Failed(await env.AssignmentHandler.AssignAsync(section.Id, new AssignInstructorCommand(inactive.Id, AssignmentRole.Primary), Req), OfferingsErrors.InstructorInactive);

            Assert.Equal(audits, env.Rbac.Audit.Entries.Count);
            Assert.Empty(env.Assignments.Items);
        }

        [Fact]
        public async Task A_section_has_one_active_primary_and_an_instructor_one_active_assignment()
        {
            var (env, versionId, semesterId) = NewWorld();
            var offering = await env.CreateOfferingAsync(versionId, semesterId);
            var section = await env.CreateSectionAsync(offering.Id);
            var lead = env.SeedInstructor();
            var other = env.SeedInstructor();
            var assistant = env.SeedInstructor();

            var primary = await env.AssignAsync(section.Id, lead.Id, AssignmentRole.Primary);

            ResultAssert.Failed(await env.AssignmentHandler.AssignAsync(section.Id, new AssignInstructorCommand(other.Id, AssignmentRole.Primary), Req), OfferingsErrors.PrimaryExists);
            ResultAssert.Failed(await env.AssignmentHandler.AssignAsync(section.Id, new AssignInstructorCommand(lead.Id, AssignmentRole.TeachingAssistant), Req), OfferingsErrors.AssignmentOverlap);

            // Co-instructors and assistants are not limited to one.
            await env.AssignAsync(section.Id, other.Id, AssignmentRole.CoInstructor);
            await env.AssignAsync(section.Id, assistant.Id, AssignmentRole.TeachingAssistant);

            // After the primary is end-dated, a new primary (even the same person) is allowed and the history stays.
            ResultAssert.Succeeded(await env.AssignmentHandler.EndAsync(section.Id, primary.Id, Req));
            await env.AssignAsync(section.Id, lead.Id, AssignmentRole.Primary);

            Assert.Equal(4, env.Assignments.Items.Count);
            Assert.Equal(1, env.Assignments.Items.Count(a => a.IsActive && a.Role == AssignmentRole.Primary));
        }

        [Fact]
        public async Task The_same_instructor_can_teach_two_sections_of_an_offering()
        {
            var (env, versionId, semesterId) = NewWorld();
            var offering = await env.CreateOfferingAsync(versionId, semesterId);
            var sectionA = await env.CreateSectionAsync(offering.Id, "A");
            var sectionB = await env.CreateSectionAsync(offering.Id, "B");
            var instructor = env.SeedInstructor();

            await env.AssignAsync(sectionA.Id, instructor.Id, AssignmentRole.Primary);
            await env.AssignAsync(sectionB.Id, instructor.Id, AssignmentRole.Primary);
        }

        [Fact]
        public async Task End_assignment_end_dates_it_once_and_checks_the_section()
        {
            var (env, versionId, semesterId) = NewWorld();
            var offering = await env.CreateOfferingAsync(versionId, semesterId);
            var section = await env.CreateSectionAsync(offering.Id, "A");
            var otherSection = await env.CreateSectionAsync(offering.Id, "B");
            var instructor = env.SeedInstructor();
            var assignment = await env.AssignAsync(section.Id, instructor.Id, AssignmentRole.CoInstructor);

            ResultAssert.Failed(await env.AssignmentHandler.EndAsync(otherSection.Id, assignment.Id, Req), OfferingsErrors.AssignmentNotFound);
            ResultAssert.Failed(await env.AssignmentHandler.EndAsync(section.Id, Guid.NewGuid(), Req), OfferingsErrors.AssignmentNotFound);

            var ended = ResultAssert.Succeeded(await env.AssignmentHandler.EndAsync(section.Id, assignment.Id, Req));
            Assert.NotNull(ended.EndedAt);
            Assert.Equal(OfferingsAuditActions.InstructorAssignmentEnded, env.Rbac.Audit.Entries.Last().Action);

            ResultAssert.Failed(await env.AssignmentHandler.EndAsync(section.Id, assignment.Id, Req), OfferingsErrors.AssignmentAlreadyEnded);
        }

        [Fact]
        public async Task A_cancelled_section_takes_no_instructors()
        {
            var (env, versionId, semesterId) = NewWorld();
            var offering = await env.CreateOfferingAsync(versionId, semesterId);
            var section = await env.CreateSectionAsync(offering.Id);
            var instructor = env.SeedInstructor();
            ResultAssert.Succeeded(await env.SectionHandler.CancelAsync(section.Id, Req));

            ResultAssert.Failed(await env.AssignmentHandler.AssignAsync(section.Id, new AssignInstructorCommand(instructor.Id, AssignmentRole.Primary), Req), OfferingsErrors.SectionNotAssignable);
        }

        [Fact]
        public async Task Assignment_list_and_permissions()
        {
            var env = new OfferingsTestEnv();
            var version = env.SeedVersion();
            var semester = env.SeedSemester();
            var home = Guid.NewGuid();
            env.PlaceDepartmentInFaculty(env.DepartmentId, home);

            env.AsDeanOf(home);
            var offering = await env.CreateOfferingAsync(version.CourseVersionId, semester.Id);
            var section = await env.CreateSectionAsync(offering.Id);
            var first = env.SeedInstructor();
            var second = env.SeedInstructor();
            var primary = await env.AssignAsync(section.Id, first.Id, AssignmentRole.Primary);
            await env.AssignAsync(section.Id, second.Id, AssignmentRole.CoInstructor);
            ResultAssert.Succeeded(await env.AssignmentHandler.EndAsync(section.Id, primary.Id, Req));

            Assert.Equal(2, ResultAssert.Succeeded(await env.AssignmentHandler.ListAsync(section.Id, activeOnly: false)).Count);
            Assert.Single(ResultAssert.Succeeded(await env.AssignmentHandler.ListAsync(section.Id, activeOnly: true)));
            ResultAssert.Failed(await env.AssignmentHandler.ListAsync(Guid.NewGuid(), false), OfferingsErrors.SectionNotFound);

            env.AsDeanOf(Guid.NewGuid());
            ResultAssert.Failed(await env.AssignmentHandler.AssignAsync(section.Id, new AssignInstructorCommand(first.Id, AssignmentRole.Primary), Req), IdentityErrors.ForbiddenOutOfScope);
            ResultAssert.Failed(await env.AssignmentHandler.EndAsync(section.Id, primary.Id, Req), IdentityErrors.ForbiddenOutOfScope);

            env.AsReader();
            ResultAssert.Failed(await env.AssignmentHandler.AssignAsync(section.Id, new AssignInstructorCommand(first.Id, AssignmentRole.Primary), Req), IdentityErrors.ForbiddenPermissionMissing);
            Assert.Equal(2, ResultAssert.Succeeded(await env.AssignmentHandler.ListAsync(section.Id, false)).Count);
        }

        [Fact]
        public async Task A_failed_save_returns_the_error_and_writes_no_audit()
        {
            var (env, versionId, semesterId) = NewWorld();
            var offering = await env.CreateOfferingAsync(versionId, semesterId);
            var audits = env.Rbac.Audit.Entries.Count;

            env.Rbac.Uow.FailWith = CommonErrors.DatabaseUpdateFailed;

            ResultAssert.Failed(await env.SectionHandler.CreateAsync(offering.Id, new CreateSectionCommand("A", 30), Req), CommonErrors.DatabaseUpdateFailed);
            Assert.Equal(audits, env.Rbac.Audit.Entries.Count);
        }

        [Fact]
        public async Task Not_signed_in_is_unauthorized()
        {
            var env = new OfferingsTestEnv();

            var result = await env.OfferingHandler.GetAsync(Guid.NewGuid());

            Assert.Equal(ErrorType.Unauthorized, result.Error?.ErrorType);
        }
    }
}
