using UniCore.Application.Common.Abstractions;
using UniCore.Application.Common.Pagination;
using UniCore.Application.Modules.Courses;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Courses;
using UniCore.Domain.Modules.Identity;
using UniCore.UnitTests.Common;
using UniCore.UnitTests.Modules.Identity;

namespace UniCore.UnitTests.Modules.Courses
{
    public class CoursesHandlersTests
    {
        private static readonly PaginationQuery FirstPage = new(1, 20);
        private static readonly RequestInfo Req = CoursesTestEnv.Request;
        private static readonly DateOnly From = CoursesTestEnv.From;
        private static readonly DateOnly Next = new(2027, 9, 1);

        private static (CoursesTestEnv Env, Guid DepartmentId, Guid CourseId) NewWorld()
        {
            var env = new CoursesTestEnv();
            var department = env.SeedDepartment();
            var course = env.SeedCourse(department.Id);
            env.AsSuperAdmin();
            return (env, department.Id, course.Id);
        }

        private static CreateCourseVersionCommand NewVersion(DateOnly? from = null, int credits = 3)
            => new(null, "Title", null, credits, 2, 2, from ?? From);

        // ---------- courses ----------

        [Fact]
        public async Task Course_create_succeeds_saves_once_and_audits()
        {
            var env = new CoursesTestEnv();
            var department = env.SeedDepartment();
            env.AsSuperAdmin();

            var summary = ResultAssert.Succeeded(await env.CourseHandler.CreateAsync(new CreateCourseCommand(department.Id, "cs101", "Intro", null, CourseType.Theory), Req));

            Assert.Equal("CS101", summary.Code);
            Assert.True(summary.IsActive);
            Assert.Equal(1, env.Rbac.Uow.SaveCalls);

            var audit = Assert.Single(env.Rbac.Audit.Entries);
            Assert.Equal(CoursesAuditActions.CourseCreated, audit.Action);
            Assert.Equal(env.ActorId, audit.ActorUserId);
            Assert.Equal(summary.Id.ToString(), audit.EntityId);
        }

        [Fact]
        public async Task Course_create_rejects_unknown_inactive_department_and_duplicate_code()
        {
            var env = new CoursesTestEnv();
            var department = env.SeedDepartment();
            var inactive = env.SeedDepartment("OLD", active: false);
            env.SeedCourse(department.Id, "CS101");
            env.AsSuperAdmin();

            ResultAssert.Failed(await env.CourseHandler.CreateAsync(new CreateCourseCommand(Guid.NewGuid(), "A1", "Course", null, CourseType.Theory), Req), CoursesErrors.DepartmentNotFound);
            ResultAssert.Failed(await env.CourseHandler.CreateAsync(new CreateCourseCommand(inactive.Id, "A1", "Course", null, CourseType.Theory), Req), CoursesErrors.DepartmentInactive);
            ResultAssert.Failed(await env.CourseHandler.CreateAsync(new CreateCourseCommand(department.Id, "cs101", "Course", null, CourseType.Theory), Req), CoursesErrors.CourseCodeExists);
            Assert.Empty(env.Rbac.Audit.Entries);
        }

        [Fact]
        public async Task The_same_code_is_allowed_in_another_department()
        {
            var env = new CoursesTestEnv();
            var first = env.SeedDepartment("CSE");
            var second = env.SeedDepartment("MATH");
            env.SeedCourse(first.Id, "101");
            env.AsSuperAdmin();

            ResultAssert.Succeeded(await env.CourseHandler.CreateAsync(new CreateCourseCommand(second.Id, "101", "Calculus", null, CourseType.Theory), Req));
        }

        [Fact]
        public async Task Course_changes_need_the_permission_and_the_departments_scope()
        {
            var env = new CoursesTestEnv();
            var facultyA = Guid.NewGuid();
            var facultyB = Guid.NewGuid();
            var department = env.SeedDepartment();
            env.PlaceDepartmentInFaculty(department.Id, facultyB);
            var course = env.SeedCourse(department.Id);

            env.AsReader();
            ResultAssert.Failed(await env.CourseHandler.CreateAsync(new CreateCourseCommand(department.Id, "NEW", "Course", null, CourseType.Theory), Req), IdentityErrors.ForbiddenPermissionMissing);
            ResultAssert.Failed(await env.CourseHandler.UpdateAsync(course.Id, new UpdateCourseCommand("Renamed", null, CourseType.Theory), Req), IdentityErrors.ForbiddenPermissionMissing);

            env.AsDeanOf(facultyA);
            ResultAssert.Failed(await env.CourseHandler.UpdateAsync(course.Id, new UpdateCourseCommand("Renamed", null, CourseType.Theory), Req), IdentityErrors.ForbiddenOutOfScope);

            env.AsDeanOf(facultyB);
            ResultAssert.Succeeded(await env.CourseHandler.UpdateAsync(course.Id, new UpdateCourseCommand("Renamed", null, CourseType.Theory), Req));
        }

        [Fact]
        public async Task Without_the_permission_an_unknown_course_looks_the_same_as_a_real_one()
        {
            var (env, _, courseId) = NewWorld();
            env.AsReader();

            ResultAssert.Failed(await env.CourseHandler.UpdateAsync(courseId, new UpdateCourseCommand("X1", null, CourseType.Theory), Req), IdentityErrors.ForbiddenPermissionMissing);
            ResultAssert.Failed(await env.CourseHandler.UpdateAsync(Guid.NewGuid(), new UpdateCourseCommand("X1", null, CourseType.Theory), Req), IdentityErrors.ForbiddenPermissionMissing);
        }

        [Fact]
        public async Task Course_deactivate_is_refused_while_a_version_is_active_and_activate_follows_the_department()
        {
            var (env, departmentId, courseId) = NewWorld();
            var active = await env.ActiveVersionAsync(courseId);

            ResultAssert.Failed(await env.CourseHandler.DeactivateAsync(courseId, Req), CoursesErrors.CourseHasActiveVersion);

            ResultAssert.Succeeded(await env.VersionHandler.RetireAsync(active.Id, new RetireCourseVersionCommand(Next), Req));
            ResultAssert.Succeeded(await env.CourseHandler.DeactivateAsync(courseId, Req));

            ResultAssert.Succeeded(env.Departments.Items.Single(d => d.Id == departmentId).Deactivate(false));
            ResultAssert.Failed(await env.CourseHandler.ActivateAsync(courseId, Req), CoursesErrors.DepartmentInactive);
        }

        [Fact]
        public async Task Course_list_get_and_sort_validation()
        {
            var (env, departmentId, courseId) = NewWorld();
            env.SeedCourse(departmentId, "OTHER");

            var page = ResultAssert.Succeeded(await env.CourseHandler.ListAsync(FirstPage, null, "-title", null, departmentId, null));
            Assert.Equal(2, page.TotalCount);
            Assert.Equal(courseId, ResultAssert.Succeeded(await env.CourseHandler.GetAsync(courseId)).Id);
            ResultAssert.Failed(await env.CourseHandler.GetAsync(Guid.NewGuid()), CoursesErrors.CourseNotFound);
            Assert.True((await env.CourseHandler.ListAsync(FirstPage, null, "isActive", null, null, null)).IsFailure);

            env.AsReader();
            Assert.Equal(2, ResultAssert.Succeeded(await env.CourseHandler.ListAsync(FirstPage, null, null, true, null, CourseType.Mixed)).TotalCount);
            Assert.Equal(0, ResultAssert.Succeeded(await env.CourseHandler.ListAsync(FirstPage, null, null, null, null, CourseType.Project)).TotalCount);

            env.Rbac.SignIn(Guid.NewGuid(), TestGrants.Role(RoleNames.Student, []));
            ResultAssert.Failed(await env.CourseHandler.ListAsync(FirstPage, null, null, null, null, null), IdentityErrors.ForbiddenPermissionMissing);
        }

        // ---------- versions: draft ----------

        [Fact]
        public async Task Version_create_numbers_versions_and_allows_one_draft_at_a_time()
        {
            var (env, _, courseId) = NewWorld();

            var first = await env.CreateDraftAsync(courseId);
            Assert.Equal(1, first.VersionNumber);
            Assert.Equal(CourseVersionStatus.Draft, first.Status);
            Assert.Equal(CoursesAuditActions.VersionCreated, env.Rbac.Audit.Entries.Last().Action);

            ResultAssert.Failed(await env.VersionHandler.CreateAsync(courseId, NewVersion(), Req), CoursesErrors.DraftExists);

            ResultAssert.Succeeded(await env.VersionHandler.ActivateAsync(first.Id, Req));

            var second = await env.CreateDraftAsync(courseId, Next);
            Assert.Equal(2, second.VersionNumber);
        }

        [Fact]
        public async Task Version_create_needs_a_date_valid_fields_and_an_active_course()
        {
            var (env, _, courseId) = NewWorld();

            ResultAssert.Failed(await env.VersionHandler.CreateAsync(courseId, new CreateCourseVersionCommand(null, "T1", null, 3, 2, 2, null), Req), CoursesErrors.EffectiveDateInvalid);
            ResultAssert.Failed(await env.VersionHandler.CreateAsync(courseId, new CreateCourseVersionCommand(null, "T1", null, null, 2, 2, From), Req), CoursesErrors.CreditsInvalid);
            ResultAssert.Failed(await env.VersionHandler.CreateAsync(Guid.NewGuid(), NewVersion(), Req), CoursesErrors.CourseNotFound);
            Assert.Empty(env.Versions.Items);

            ResultAssert.Succeeded(env.Courses.Items.Single().Deactivate(false, env.Clock.UtcNow));
            ResultAssert.Failed(await env.VersionHandler.CreateAsync(courseId, NewVersion(), Req), CoursesErrors.CourseInactive);
        }

        [Fact]
        public async Task A_new_version_copies_the_source_and_only_changes_what_is_sent()
        {
            var (env, _, courseId) = NewWorld();
            var active = await env.ActiveVersionAsync(courseId);

            var copy = ResultAssert.Succeeded(await env.VersionHandler.CreateAsync(courseId, new CreateCourseVersionCommand(active.Id, null, null, 4, null, null, Next), Req));

            Assert.Equal(2, copy.VersionNumber);
            Assert.Equal(4, copy.Credits);
            Assert.Equal(active.Title, copy.Title);
            Assert.Equal(active.LectureHours, copy.LectureHours);
            Assert.Equal(active.PracticalHours, copy.PracticalHours);
            Assert.Contains("CopiedFrom=", env.Rbac.Audit.Entries.Last().NewValues);

            // The history is untouched.
            var original = env.Versions.Items.Single(v => v.Id == active.Id);
            Assert.Equal(3, original.Credits);
            Assert.Equal(CourseVersionStatus.Active, original.Status);
        }

        [Fact]
        public async Task A_source_of_another_course_or_an_unknown_source_is_refused()
        {
            var (env, departmentId, courseId) = NewWorld();
            var other = env.SeedCourse(departmentId, "OTHER");
            var otherActive = await env.ActiveVersionAsync(other.Id);

            ResultAssert.Failed(await env.VersionHandler.CreateAsync(courseId, new CreateCourseVersionCommand(otherActive.Id, null, null, null, null, null, Next), Req), CoursesErrors.VersionNotFound);
            ResultAssert.Failed(await env.VersionHandler.CreateAsync(courseId, new CreateCourseVersionCommand(Guid.NewGuid(), null, null, null, null, null, Next), Req), CoursesErrors.VersionNotFound);
        }

        [Fact]
        public async Task Version_update_changes_a_draft_only()
        {
            var (env, _, courseId) = NewWorld();
            var draft = await env.CreateDraftAsync(courseId);

            var updated = ResultAssert.Succeeded(await env.VersionHandler.UpdateAsync(draft.Id, new UpdateCourseVersionCommand("Programming 1", null, 4, 3, 2, new DateOnly(2027, 2, 1)), Req));
            Assert.Equal("Programming 1", updated.Title);
            Assert.Equal(4, updated.Credits);
            Assert.Equal(new DateOnly(2027, 2, 1), updated.EffectiveFrom);

            ResultAssert.Failed(await env.VersionHandler.UpdateAsync(draft.Id, new UpdateCourseVersionCommand("Programming 1", null, 4, 3, 2, null), Req), CoursesErrors.EffectiveDateInvalid);
            ResultAssert.Failed(await env.VersionHandler.UpdateAsync(draft.Id, new UpdateCourseVersionCommand("Programming 1", null, 0, 3, 2, From), Req), CoursesErrors.CreditsInvalid);

            ResultAssert.Succeeded(await env.VersionHandler.ActivateAsync(draft.Id, Req));
            ResultAssert.Failed(await env.VersionHandler.UpdateAsync(draft.Id, new UpdateCourseVersionCommand("Changed", null, 4, 3, 2, From), Req), CoursesErrors.VersionNotDraft);
        }

        // ---------- versions: lifecycle ----------

        [Fact]
        public async Task Activate_retires_the_previous_version_the_day_before_and_audits_both()
        {
            var (env, _, courseId) = NewWorld();
            var first = await env.ActiveVersionAsync(courseId, From);
            var draft = await env.CreateDraftAsync(courseId, Next, first.Id);

            var saves = env.Rbac.Uow.SaveCalls;
            var activated = ResultAssert.Succeeded(await env.VersionHandler.ActivateAsync(draft.Id, Req));

            Assert.Equal(CourseVersionStatus.Active, activated.Status);
            Assert.Equal(saves + 1, env.Rbac.Uow.SaveCalls);

            var previous = env.Versions.Items.Single(v => v.Id == first.Id);
            Assert.Equal(CourseVersionStatus.Retired, previous.Status);
            Assert.Equal(new DateOnly(2027, 8, 31), previous.EffectiveTo);

            var tail = env.Rbac.Audit.Entries.TakeLast(2).Select(e => e.Action).ToArray();
            Assert.Equal(new[] { CoursesAuditActions.VersionActivated, CoursesAuditActions.VersionRetired }, tail);
        }

        [Fact]
        public async Task Activate_refuses_a_version_that_does_not_start_after_the_one_it_replaces()
        {
            var (env, _, courseId) = NewWorld();
            var first = await env.ActiveVersionAsync(courseId, From);
            var draft = await env.CreateDraftAsync(courseId, From, first.Id);

            ResultAssert.Failed(await env.VersionHandler.ActivateAsync(draft.Id, Req), CoursesErrors.EffectiveDateInvalid);

            Assert.Equal(CourseVersionStatus.Draft, env.Versions.Items.Single(v => v.Id == draft.Id).Status);
            Assert.Equal(CourseVersionStatus.Active, env.Versions.Items.Single(v => v.Id == first.Id).Status);
        }

        [Fact]
        public async Task Activate_refuses_an_overlap_with_an_earlier_retired_version()
        {
            var (env, _, courseId) = NewWorld();
            var first = await env.ActiveVersionAsync(courseId, From);
            ResultAssert.Succeeded(await env.VersionHandler.RetireAsync(first.Id, new RetireCourseVersionCommand(new DateOnly(2027, 8, 31)), Req));

            var overlapping = await env.CreateDraftAsync(courseId, new DateOnly(2027, 1, 1), first.Id);
            ResultAssert.Failed(await env.VersionHandler.ActivateAsync(overlapping.Id, Req), CoursesErrors.VersionOverlap);

            ResultAssert.Succeeded(await env.VersionHandler.UpdateAsync(overlapping.Id, new UpdateCourseVersionCommand(overlapping.Title, null, overlapping.Credits, overlapping.LectureHours, overlapping.PracticalHours, Next), Req));
            ResultAssert.Succeeded(await env.VersionHandler.ActivateAsync(overlapping.Id, Req));
        }

        [Fact]
        public async Task Activate_fails_for_a_version_that_is_not_a_draft_or_in_an_inactive_course()
        {
            var (env, _, courseId) = NewWorld();
            var active = await env.ActiveVersionAsync(courseId);

            ResultAssert.Failed(await env.VersionHandler.ActivateAsync(active.Id, Req), CoursesErrors.VersionInvalidState);
            ResultAssert.Failed(await env.VersionHandler.ActivateAsync(Guid.NewGuid(), Req), CoursesErrors.VersionNotFound);

            var draft = await env.CreateDraftAsync(courseId, Next);
            ResultAssert.Succeeded(env.Courses.Items.Single().Deactivate(false, env.Clock.UtcNow));
            ResultAssert.Failed(await env.VersionHandler.ActivateAsync(draft.Id, Req), CoursesErrors.CourseInactive);
        }

        [Fact]
        public async Task Retire_needs_an_end_date_that_is_not_before_the_start()
        {
            var (env, _, courseId) = NewWorld();
            var active = await env.ActiveVersionAsync(courseId, From);

            ResultAssert.Failed(await env.VersionHandler.RetireAsync(active.Id, new RetireCourseVersionCommand(null), Req), CoursesErrors.EffectiveToInvalid);
            ResultAssert.Failed(await env.VersionHandler.RetireAsync(active.Id, new RetireCourseVersionCommand(From.AddDays(-1)), Req), CoursesErrors.EffectiveToInvalid);

            var retired = ResultAssert.Succeeded(await env.VersionHandler.RetireAsync(active.Id, new RetireCourseVersionCommand(Next), Req));
            Assert.Equal(CourseVersionStatus.Retired, retired.Status);
            Assert.Equal(CoursesAuditActions.VersionRetired, env.Rbac.Audit.Entries.Last().Action);
        }

        [Fact]
        public async Task A_failed_save_returns_the_error_and_writes_no_audit()
        {
            var (env, _, courseId) = NewWorld();
            var draft = await env.CreateDraftAsync(courseId);
            var audits = env.Rbac.Audit.Entries.Count;

            env.Rbac.Uow.FailWith = CommonErrors.DatabaseUpdateFailed;

            ResultAssert.Failed(await env.VersionHandler.ActivateAsync(draft.Id, Req), CommonErrors.DatabaseUpdateFailed);
            Assert.Equal(audits, env.Rbac.Audit.Entries.Count);
        }

        // ---------- permissions and scope on versions ----------

        [Fact]
        public async Task Version_changes_are_limited_to_the_deans_faculty_and_reads_need_courses_read()
        {
            var env = new CoursesTestEnv();
            var home = Guid.NewGuid();
            var department = env.SeedDepartment();
            env.PlaceDepartmentInFaculty(department.Id, home);
            var course = env.SeedCourse(department.Id);

            env.AsDeanOf(home);
            var draft = await env.CreateDraftAsync(course.Id);

            env.AsDeanOf(Guid.NewGuid());
            ResultAssert.Failed(await env.VersionHandler.ActivateAsync(draft.Id, Req), IdentityErrors.ForbiddenOutOfScope);
            ResultAssert.Failed(await env.VersionHandler.CreateAsync(course.Id, NewVersion(Next), Req), IdentityErrors.ForbiddenOutOfScope);

            env.AsReader();
            ResultAssert.Failed(await env.VersionHandler.ActivateAsync(draft.Id, Req), IdentityErrors.ForbiddenPermissionMissing);
            ResultAssert.Succeeded(await env.VersionHandler.GetAsync(draft.Id));
            Assert.Single(ResultAssert.Succeeded(await env.VersionHandler.ListAsync(course.Id)));

            env.Rbac.SignIn(Guid.NewGuid(), TestGrants.Role(RoleNames.Student, []));
            ResultAssert.Failed(await env.VersionHandler.GetAsync(draft.Id), IdentityErrors.ForbiddenPermissionMissing);
        }

        [Fact]
        public async Task Version_get_and_list_report_unknown_ids()
        {
            var (env, _, courseId) = NewWorld();

            ResultAssert.Failed(await env.VersionHandler.GetAsync(Guid.NewGuid()), CoursesErrors.VersionNotFound);
            ResultAssert.Failed(await env.VersionHandler.ListAsync(Guid.NewGuid()), CoursesErrors.CourseNotFound);

            await env.CreateDraftAsync(courseId);
            Assert.Single(ResultAssert.Succeeded(await env.VersionHandler.ListAsync(courseId)));
        }

        [Fact]
        public async Task Not_signed_in_is_unauthorized()
        {
            var env = new CoursesTestEnv();

            var result = await env.VersionHandler.GetAsync(Guid.NewGuid());

            Assert.Equal(ErrorType.Unauthorized, result.Error?.ErrorType);
        }
    }
}
