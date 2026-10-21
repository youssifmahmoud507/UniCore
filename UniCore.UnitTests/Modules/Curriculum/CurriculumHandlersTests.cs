using UniCore.Application.Common.Abstractions;
using UniCore.Application.Common.Pagination;
using UniCore.Application.Modules.Curriculum;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Curriculum;
using UniCore.Domain.Modules.Identity;
using UniCore.UnitTests.Common;
using UniCore.UnitTests.Modules.Identity;

namespace UniCore.UnitTests.Modules.Curriculum
{
    public class CurriculumHandlersTests
    {
        private static readonly PaginationQuery FirstPage = new(1, 20);
        private static readonly RequestInfo Req = CurriculumTestEnv.Request;
        private static readonly DateOnly From = new(2026, 9, 1);

        private static (CurriculumTestEnv Env, Guid ProgramId, Guid PlanId) NewWorld()
        {
            var env = new CurriculumTestEnv();
            var program = env.SeedProgram();
            var plan = env.SeedPlan(program.Id);
            env.AsSuperAdmin();
            return (env, program.Id, plan.Id);
        }

        // ---------- study plans ----------

        [Fact]
        public async Task Plan_create_succeeds_saves_once_and_audits()
        {
            var env = new CurriculumTestEnv();
            var program = env.SeedProgram();
            env.AsSuperAdmin();

            var summary = ResultAssert.Succeeded(await env.PlanHandler.CreateAsync(new CreateStudyPlanCommand(program.Id, "cse-2026", "CSE plan", null), Req));

            Assert.Equal("CSE-2026", summary.Code);
            Assert.True(summary.IsActive);
            Assert.Equal(1, env.Rbac.Uow.SaveCalls);

            var audit = Assert.Single(env.Rbac.Audit.Entries);
            Assert.Equal(CurriculumAuditActions.StudyPlanCreated, audit.Action);
            Assert.Equal(env.ActorId, audit.ActorUserId);
            Assert.Equal(summary.Id.ToString(), audit.EntityId);
        }

        [Fact]
        public async Task Plan_create_rejects_unknown_inactive_program_and_duplicate_code()
        {
            var env = new CurriculumTestEnv();
            var program = env.SeedProgram();
            var inactive = env.SeedProgram(code: "OLD", active: false);
            env.SeedPlan(program.Id, "CSE-2026");
            env.AsSuperAdmin();

            ResultAssert.Failed(await env.PlanHandler.CreateAsync(new CreateStudyPlanCommand(Guid.NewGuid(), "A1", "Plan", null), Req), CurriculumErrors.ProgramNotFound);
            ResultAssert.Failed(await env.PlanHandler.CreateAsync(new CreateStudyPlanCommand(inactive.Id, "A1", "Plan", null), Req), CurriculumErrors.ProgramInactive);
            ResultAssert.Failed(await env.PlanHandler.CreateAsync(new CreateStudyPlanCommand(program.Id, "cse-2026", "Plan", null), Req), CurriculumErrors.PlanCodeExists);
            Assert.Empty(env.Rbac.Audit.Entries);
        }

        [Fact]
        public async Task Plan_changes_need_the_permission_and_the_programs_scope()
        {
            var env = new CurriculumTestEnv();
            var facultyA = Guid.NewGuid();
            var facultyB = Guid.NewGuid();
            var departmentB = Guid.NewGuid();
            env.PlaceDepartmentInFaculty(departmentB, facultyB);
            var program = env.SeedProgram(departmentB);
            var plan = env.SeedPlan(program.Id);

            env.AsReader();
            ResultAssert.Failed(await env.PlanHandler.CreateAsync(new CreateStudyPlanCommand(program.Id, "NEW", "Plan", null), Req), IdentityErrors.ForbiddenPermissionMissing);
            ResultAssert.Failed(await env.PlanHandler.UpdateAsync(plan.Id, new UpdateStudyPlanCommand("Renamed", null), Req), IdentityErrors.ForbiddenPermissionMissing);

            env.AsDeanOf(facultyA);
            ResultAssert.Failed(await env.PlanHandler.UpdateAsync(plan.Id, new UpdateStudyPlanCommand("Renamed", null), Req), IdentityErrors.ForbiddenOutOfScope);

            env.AsDeanOf(facultyB);
            ResultAssert.Succeeded(await env.PlanHandler.UpdateAsync(plan.Id, new UpdateStudyPlanCommand("Renamed", null), Req));
        }

        [Fact]
        public async Task Without_the_permission_an_unknown_plan_looks_the_same_as_a_real_one()
        {
            var (env, _, planId) = NewWorld();
            env.AsReader();

            ResultAssert.Failed(await env.PlanHandler.UpdateAsync(planId, new UpdateStudyPlanCommand("X1", null), Req), IdentityErrors.ForbiddenPermissionMissing);
            ResultAssert.Failed(await env.PlanHandler.UpdateAsync(Guid.NewGuid(), new UpdateStudyPlanCommand("X1", null), Req), IdentityErrors.ForbiddenPermissionMissing);
        }

        [Fact]
        public async Task Plan_deactivate_is_refused_while_a_version_is_active_and_activate_follows_the_program()
        {
            var (env, programId, planId) = NewWorld();
            var active = await env.ActiveVersionAsync(planId);

            ResultAssert.Failed(await env.PlanHandler.DeactivateAsync(planId, Req), CurriculumErrors.PlanHasActiveVersion);

            ResultAssert.Succeeded(await env.VersionHandler.RetireAsync(active.Version.Id, new RetireVersionCommand(new DateOnly(2027, 8, 31)), Req));
            ResultAssert.Succeeded(await env.PlanHandler.DeactivateAsync(planId, Req));

            env.Programs.Items.Single(p => p.Id == programId).Deactivate(false, env.Clock.UtcNow);
            ResultAssert.Failed(await env.PlanHandler.ActivateAsync(planId, Req), CurriculumErrors.ProgramInactive);
        }

        [Fact]
        public async Task Plan_list_get_and_sort_validation()
        {
            var (env, programId, planId) = NewWorld();
            env.SeedPlan(programId, "OTHER");

            var page = ResultAssert.Succeeded(await env.PlanHandler.ListAsync(FirstPage, null, "-name", null, programId));
            Assert.Equal(2, page.TotalCount);
            Assert.Equal(planId, ResultAssert.Succeeded(await env.PlanHandler.GetAsync(planId)).Id);
            ResultAssert.Failed(await env.PlanHandler.GetAsync(Guid.NewGuid()), CurriculumErrors.PlanNotFound);
            Assert.True((await env.PlanHandler.ListAsync(FirstPage, null, "isActive", null, null)).IsFailure);

            env.AsReader();
            Assert.Equal(2, ResultAssert.Succeeded(await env.PlanHandler.ListAsync(FirstPage, null, null, true, null)).TotalCount);

            env.Rbac.SignIn(Guid.NewGuid(), TestGrants.Role(RoleNames.Student, []));
            ResultAssert.Failed(await env.PlanHandler.ListAsync(FirstPage, null, null, null, null), IdentityErrors.ForbiddenPermissionMissing);
        }

        // ---------- versions: draft ----------

        [Fact]
        public async Task Version_create_numbers_versions_and_allows_one_draft_at_a_time()
        {
            var (env, _, planId) = NewWorld();

            var first = await env.CreateDraftAsync(planId);
            Assert.Equal(1, first.Version.VersionNumber);
            Assert.Equal(StudyPlanVersionStatus.Draft, first.Version.Status);
            Assert.Equal(CurriculumAuditActions.VersionCreated, env.Rbac.Audit.Entries.Last().Action);

            ResultAssert.Failed(await env.VersionHandler.CreateAsync(planId, new CreateVersionCommand(null, From), Req), CurriculumErrors.DraftExists);

            await env.AddCourseAsync(first.Version.Id);
            ResultAssert.Succeeded(await env.VersionHandler.ActivateAsync(first.Version.Id, Req));

            var second = await env.CreateDraftAsync(planId, new DateOnly(2027, 9, 1));
            Assert.Equal(2, second.Version.VersionNumber);
        }

        [Fact]
        public async Task Version_create_needs_a_date_an_active_plan_and_an_active_program()
        {
            var (env, programId, planId) = NewWorld();

            ResultAssert.Failed(await env.VersionHandler.CreateAsync(planId, new CreateVersionCommand(null, null), Req), CurriculumErrors.EffectiveDateInvalid);
            ResultAssert.Failed(await env.VersionHandler.CreateAsync(Guid.NewGuid(), new CreateVersionCommand(null, From), Req), CurriculumErrors.PlanNotFound);

            env.Plans.Items.Single().Deactivate(false, env.Clock.UtcNow);
            ResultAssert.Failed(await env.VersionHandler.CreateAsync(planId, new CreateVersionCommand(null, From), Req), CurriculumErrors.PlanInactive);

            env.Plans.Items.Single().Activate(env.Clock.UtcNow);
            env.Programs.Items.Single(p => p.Id == programId).Deactivate(false, env.Clock.UtcNow);
            ResultAssert.Failed(await env.VersionHandler.CreateAsync(planId, new CreateVersionCommand(null, From), Req), CurriculumErrors.ProgramInactive);
        }

        [Fact]
        public async Task Version_create_from_a_source_copies_it_and_rejects_a_source_of_another_plan()
        {
            var (env, programId, planId) = NewWorld();
            var otherPlan = env.SeedPlan(programId, "OTHER");

            var active = await env.ActiveVersionAsync(planId);
            var otherActive = await env.ActiveVersionAsync(otherPlan.Id);

            ResultAssert.Failed(await env.VersionHandler.CreateAsync(planId, new CreateVersionCommand(otherActive.Version.Id, new DateOnly(2027, 9, 1)), Req), CurriculumErrors.VersionNotFound);
            ResultAssert.Failed(await env.VersionHandler.CreateAsync(planId, new CreateVersionCommand(Guid.NewGuid(), new DateOnly(2027, 9, 1)), Req), CurriculumErrors.VersionNotFound);

            var copy = ResultAssert.Succeeded(await env.VersionHandler.CreateAsync(planId, new CreateVersionCommand(active.Version.Id, new DateOnly(2027, 9, 1)), Req));

            Assert.Equal(2, copy.Version.VersionNumber);
            Assert.Equal(active.Courses.Select(c => c.CourseVersionId), copy.Courses.Select(c => c.CourseVersionId));
            Assert.Contains("CopiedFrom=", env.Rbac.Audit.Entries.Last().NewValues);
        }

        [Fact]
        public async Task Version_update_changes_the_date_of_a_draft_only()
        {
            var (env, _, planId) = NewWorld();
            var draft = await env.CreateDraftAsync(planId);

            var updated = ResultAssert.Succeeded(await env.VersionHandler.UpdateAsync(draft.Version.Id, new UpdateVersionCommand(new DateOnly(2027, 2, 1)), Req));
            Assert.Equal(new DateOnly(2027, 2, 1), updated.Version.EffectiveFrom);

            ResultAssert.Failed(await env.VersionHandler.UpdateAsync(draft.Version.Id, new UpdateVersionCommand(null), Req), CurriculumErrors.EffectiveDateInvalid);

            await env.AddCourseAsync(draft.Version.Id);
            ResultAssert.Succeeded(await env.VersionHandler.ActivateAsync(draft.Version.Id, Req));
            ResultAssert.Failed(await env.VersionHandler.UpdateAsync(draft.Version.Id, new UpdateVersionCommand(From), Req), CurriculumErrors.VersionNotDraft);
        }

        // ---------- versions: courses and requirements ----------

        [Fact]
        public async Task Add_course_stores_it_and_audits()
        {
            var (env, programId, planId) = NewWorld();
            var level = env.SeedLevel(programId);
            var term = env.SeedTerm();
            var draft = await env.CreateDraftAsync(planId);
            var courseVersion = Guid.NewGuid();

            var detail = ResultAssert.Succeeded(await env.VersionHandler.AddCourseAsync(draft.Version.Id, new AddPlanCourseCommand(courseVersion, level.Id, term.Id, CourseRequirementType.Elective, 3), Req));

            var course = Assert.Single(detail.Courses);
            Assert.Equal(courseVersion, course.CourseVersionId);
            Assert.Equal(level.Id, course.AcademicLevelId);
            Assert.Equal(term.Id, course.RecommendedTermId);
            Assert.Equal(CourseRequirementType.Elective, course.RequirementType);
            Assert.Equal(CurriculumAuditActions.CourseAdded, env.Rbac.Audit.Entries.Last().Action);
        }

        [Fact]
        public async Task Add_course_rejects_bad_references()
        {
            var (env, programId, planId) = NewWorld();
            var otherProgram = env.SeedProgram(code: "OTHER");
            var foreignLevel = env.SeedLevel(otherProgram.Id);
            var inactiveLevel = env.SeedLevel(programId, order: 2, active: false);
            var inactiveTerm = env.SeedTerm("OLD", active: false);
            var draft = await env.CreateDraftAsync(planId);
            var id = draft.Version.Id;
            var unknownCourse = Guid.NewGuid();
            env.CourseVersions.Unknown.Add(unknownCourse);

            ResultAssert.Failed(await env.VersionHandler.AddCourseAsync(id, new AddPlanCourseCommand(unknownCourse, null, null, CourseRequirementType.Required, null), Req), CurriculumErrors.CourseVersionNotFound);
            ResultAssert.Failed(await env.VersionHandler.AddCourseAsync(id, new AddPlanCourseCommand(Guid.Empty, null, null, CourseRequirementType.Required, null), Req), CurriculumErrors.CourseVersionInvalid);
            ResultAssert.Failed(await env.VersionHandler.AddCourseAsync(id, new AddPlanCourseCommand(Guid.NewGuid(), null, null, null, null), Req), CurriculumErrors.RequirementTypeInvalid);
            ResultAssert.Failed(await env.VersionHandler.AddCourseAsync(id, new AddPlanCourseCommand(Guid.NewGuid(), Guid.NewGuid(), null, CourseRequirementType.Required, null), Req), CurriculumErrors.LevelNotFound);
            ResultAssert.Failed(await env.VersionHandler.AddCourseAsync(id, new AddPlanCourseCommand(Guid.NewGuid(), foreignLevel.Id, null, CourseRequirementType.Required, null), Req), CurriculumErrors.LevelInvalid);
            ResultAssert.Failed(await env.VersionHandler.AddCourseAsync(id, new AddPlanCourseCommand(Guid.NewGuid(), inactiveLevel.Id, null, CourseRequirementType.Required, null), Req), CurriculumErrors.LevelInvalid);
            ResultAssert.Failed(await env.VersionHandler.AddCourseAsync(id, new AddPlanCourseCommand(Guid.NewGuid(), null, Guid.NewGuid(), CourseRequirementType.Required, null), Req), CurriculumErrors.TermNotFound);
            ResultAssert.Failed(await env.VersionHandler.AddCourseAsync(id, new AddPlanCourseCommand(Guid.NewGuid(), null, inactiveTerm.Id, CourseRequirementType.Required, null), Req), CurriculumErrors.TermInactive);

            Assert.Empty(env.Versions.Items.Single().Courses);
        }

        [Fact]
        public async Task Add_course_rejects_a_duplicate_and_an_active_version()
        {
            var (env, _, planId) = NewWorld();
            var draft = await env.CreateDraftAsync(planId);
            var course = Guid.NewGuid();
            await env.AddCourseAsync(draft.Version.Id, course);

            ResultAssert.Failed(await env.VersionHandler.AddCourseAsync(draft.Version.Id, new AddPlanCourseCommand(course, null, null, CourseRequirementType.Required, null), Req), CurriculumErrors.CourseDuplicate);

            ResultAssert.Succeeded(await env.VersionHandler.ActivateAsync(draft.Version.Id, Req));
            ResultAssert.Failed(await env.VersionHandler.AddCourseAsync(draft.Version.Id, new AddPlanCourseCommand(Guid.NewGuid(), null, null, CourseRequirementType.Required, null), Req), CurriculumErrors.VersionNotDraft);
        }

        [Fact]
        public async Task Update_and_remove_course()
        {
            var (env, _, planId) = NewWorld();
            var draft = await env.CreateDraftAsync(planId);
            var detail = await env.AddCourseAsync(draft.Version.Id, type: CourseRequirementType.Elective);
            var courseId = detail.Courses.Single().Id;

            var updated = ResultAssert.Succeeded(await env.VersionHandler.UpdateCourseAsync(draft.Version.Id, courseId, new UpdatePlanCourseCommand(null, null, CourseRequirementType.Required, 4), Req));
            Assert.Equal(CourseRequirementType.Required, updated.Courses.Single().RequirementType);
            Assert.Equal(4, updated.Courses.Single().RequiredCredits);

            ResultAssert.Failed(await env.VersionHandler.UpdateCourseAsync(draft.Version.Id, courseId, new UpdatePlanCourseCommand(null, null, null, null), Req), CurriculumErrors.RequirementTypeInvalid);
            ResultAssert.Failed(await env.VersionHandler.UpdateCourseAsync(draft.Version.Id, Guid.NewGuid(), new UpdatePlanCourseCommand(null, null, CourseRequirementType.Required, null), Req), CurriculumErrors.CourseNotFound);

            var removed = ResultAssert.Succeeded(await env.VersionHandler.RemoveCourseAsync(draft.Version.Id, courseId, Req));
            Assert.Empty(removed.Courses);
            Assert.Equal(CurriculumAuditActions.CourseRemoved, env.Rbac.Audit.Entries.Last().Action);
            ResultAssert.Failed(await env.VersionHandler.RemoveCourseAsync(draft.Version.Id, courseId, Req), CurriculumErrors.CourseNotFound);
        }

        [Fact]
        public async Task Set_and_remove_requirement()
        {
            var (env, _, planId) = NewWorld();
            var draft = await env.CreateDraftAsync(planId);
            var courseVersion = Guid.NewGuid();
            await env.AddCourseAsync(draft.Version.Id, courseVersion, CourseRequirementType.Elective);

            ResultAssert.Failed(await env.VersionHandler.SetRequirementAsync(draft.Version.Id, new SetRequirementCommand(courseVersion, null), Req), CurriculumErrors.RequirementTypeInvalid);
            ResultAssert.Failed(await env.VersionHandler.SetRequirementAsync(draft.Version.Id, new SetRequirementCommand(Guid.NewGuid(), CourseRequirementType.Required), Req), CurriculumErrors.CourseNotFound);

            var set = ResultAssert.Succeeded(await env.VersionHandler.SetRequirementAsync(draft.Version.Id, new SetRequirementCommand(courseVersion, CourseRequirementType.Required), Req));
            Assert.Equal(CourseRequirementType.Required, Assert.Single(set.Requirements).RequirementType);
            Assert.Equal(CurriculumAuditActions.RequirementSet, env.Rbac.Audit.Entries.Last().Action);

            ResultAssert.Succeeded(await env.VersionHandler.RemoveRequirementAsync(draft.Version.Id, courseVersion, Req));
            ResultAssert.Failed(await env.VersionHandler.RemoveRequirementAsync(draft.Version.Id, courseVersion, Req), CurriculumErrors.RequirementNotFound);
        }

        // ---------- versions: lifecycle ----------

        [Fact]
        public async Task Activate_needs_a_course()
        {
            var (env, _, planId) = NewWorld();
            var draft = await env.CreateDraftAsync(planId);

            ResultAssert.Failed(await env.VersionHandler.ActivateAsync(draft.Version.Id, Req), CurriculumErrors.VersionEmpty);
            Assert.Equal(StudyPlanVersionStatus.Draft, env.Versions.Items.Single().Status);
        }

        [Fact]
        public async Task Activate_retires_the_previous_version_the_day_before_and_audits_both()
        {
            var (env, _, planId) = NewWorld();
            var first = await env.ActiveVersionAsync(planId, From);
            var draft = await env.CreateDraftAsync(planId, new DateOnly(2027, 9, 1), first.Version.Id);

            var saves = env.Rbac.Uow.SaveCalls;
            var activated = ResultAssert.Succeeded(await env.VersionHandler.ActivateAsync(draft.Version.Id, Req));

            Assert.Equal(StudyPlanVersionStatus.Active, activated.Version.Status);
            Assert.Equal(saves + 1, env.Rbac.Uow.SaveCalls);

            var previous = env.Versions.Items.Single(v => v.Id == first.Version.Id);
            Assert.Equal(StudyPlanVersionStatus.Retired, previous.Status);
            Assert.Equal(new DateOnly(2027, 8, 31), previous.EffectiveTo);

            var tail = env.Rbac.Audit.Entries.TakeLast(2).Select(e => e.Action).ToArray();
            Assert.Equal(new[] { CurriculumAuditActions.VersionActivated, CurriculumAuditActions.VersionRetired }, tail);
        }

        [Fact]
        public async Task Activate_refuses_a_version_that_does_not_start_after_the_one_it_replaces()
        {
            var (env, _, planId) = NewWorld();
            var first = await env.ActiveVersionAsync(planId, From);
            var draft = await env.CreateDraftAsync(planId, From, first.Version.Id);

            ResultAssert.Failed(await env.VersionHandler.ActivateAsync(draft.Version.Id, Req), CurriculumErrors.EffectiveDateInvalid);

            Assert.Equal(StudyPlanVersionStatus.Draft, env.Versions.Items.Single(v => v.Id == draft.Version.Id).Status);
            Assert.Equal(StudyPlanVersionStatus.Active, env.Versions.Items.Single(v => v.Id == first.Version.Id).Status);
        }

        [Fact]
        public async Task Activate_fails_for_a_version_that_is_not_a_draft_or_in_an_inactive_plan()
        {
            var (env, _, planId) = NewWorld();
            var active = await env.ActiveVersionAsync(planId);

            ResultAssert.Failed(await env.VersionHandler.ActivateAsync(active.Version.Id, Req), CurriculumErrors.VersionInvalidState);
            ResultAssert.Failed(await env.VersionHandler.ActivateAsync(Guid.NewGuid(), Req), CurriculumErrors.VersionNotFound);

            var draft = await env.CreateDraftAsync(planId, new DateOnly(2027, 9, 1));
            await env.AddCourseAsync(draft.Version.Id);
            env.Plans.Items.Single().Deactivate(false, env.Clock.UtcNow);
            ResultAssert.Failed(await env.VersionHandler.ActivateAsync(draft.Version.Id, Req), CurriculumErrors.PlanInactive);
        }

        [Fact]
        public async Task Retire_needs_an_end_date_that_is_not_before_the_start()
        {
            var (env, _, planId) = NewWorld();
            var active = await env.ActiveVersionAsync(planId, From);

            ResultAssert.Failed(await env.VersionHandler.RetireAsync(active.Version.Id, new RetireVersionCommand(null), Req), CurriculumErrors.EffectiveToInvalid);
            ResultAssert.Failed(await env.VersionHandler.RetireAsync(active.Version.Id, new RetireVersionCommand(From.AddDays(-1)), Req), CurriculumErrors.EffectiveToInvalid);

            var retired = ResultAssert.Succeeded(await env.VersionHandler.RetireAsync(active.Version.Id, new RetireVersionCommand(new DateOnly(2027, 8, 31)), Req));
            Assert.Equal(StudyPlanVersionStatus.Retired, retired.Version.Status);
            Assert.Equal(CurriculumAuditActions.VersionRetired, env.Rbac.Audit.Entries.Last().Action);
        }

        [Fact]
        public async Task A_failed_save_returns_the_error_and_writes_no_audit()
        {
            var (env, _, planId) = NewWorld();
            var draft = await env.CreateDraftAsync(planId);
            var audits = env.Rbac.Audit.Entries.Count;

            env.Rbac.Uow.FailWith = CommonErrors.DatabaseUpdateFailed;

            ResultAssert.Failed(await env.VersionHandler.AddCourseAsync(draft.Version.Id, new AddPlanCourseCommand(Guid.NewGuid(), null, null, CourseRequirementType.Required, null), Req), CommonErrors.DatabaseUpdateFailed);
            Assert.Equal(audits, env.Rbac.Audit.Entries.Count);
        }

        // ---------- permissions and scope on versions ----------

        [Fact]
        public async Task Version_changes_are_limited_to_the_deans_faculty_and_reads_need_courses_read()
        {
            var env = new CurriculumTestEnv();
            var home = Guid.NewGuid();
            var departmentId = Guid.NewGuid();
            env.PlaceDepartmentInFaculty(departmentId, home);
            var program = env.SeedProgram(departmentId);
            var plan = env.SeedPlan(program.Id);

            env.AsDeanOf(home);
            var draft = await env.CreateDraftAsync(plan.Id);

            env.AsDeanOf(Guid.NewGuid());
            ResultAssert.Failed(await env.VersionHandler.AddCourseAsync(draft.Version.Id, new AddPlanCourseCommand(Guid.NewGuid(), null, null, CourseRequirementType.Required, null), Req), IdentityErrors.ForbiddenOutOfScope);
            ResultAssert.Failed(await env.VersionHandler.CreateAsync(plan.Id, new CreateVersionCommand(null, From), Req), IdentityErrors.ForbiddenOutOfScope);

            env.AsReader();
            ResultAssert.Failed(await env.VersionHandler.AddCourseAsync(draft.Version.Id, new AddPlanCourseCommand(Guid.NewGuid(), null, null, CourseRequirementType.Required, null), Req), IdentityErrors.ForbiddenPermissionMissing);
            ResultAssert.Succeeded(await env.VersionHandler.GetAsync(draft.Version.Id));
            Assert.Single(ResultAssert.Succeeded(await env.VersionHandler.ListAsync(plan.Id)));

            env.Rbac.SignIn(Guid.NewGuid(), TestGrants.Role(RoleNames.Student, []));
            ResultAssert.Failed(await env.VersionHandler.GetAsync(draft.Version.Id), IdentityErrors.ForbiddenPermissionMissing);
        }

        [Fact]
        public async Task Version_get_and_list_report_unknown_ids()
        {
            var (env, _, planId) = NewWorld();

            ResultAssert.Failed(await env.VersionHandler.GetAsync(Guid.NewGuid()), CurriculumErrors.VersionNotFound);
            ResultAssert.Failed(await env.VersionHandler.ListAsync(Guid.NewGuid()), CurriculumErrors.PlanNotFound);

            await env.CreateDraftAsync(planId);
            var listed = ResultAssert.Succeeded(await env.VersionHandler.ListAsync(planId));
            Assert.Single(listed);
        }

        [Fact]
        public async Task Not_signed_in_is_unauthorized()
        {
            var env = new CurriculumTestEnv();

            var result = await env.VersionHandler.GetAsync(Guid.NewGuid());

            Assert.Equal(ErrorType.Unauthorized, result.Error?.ErrorType);
        }
    }
}
