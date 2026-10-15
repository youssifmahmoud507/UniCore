using UniCore.Application.Common.Pagination;
using UniCore.Application.Modules.University;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.University;
using UniCore.UnitTests.Common;

namespace UniCore.UnitTests.Modules.University
{
    public class ProgramManagementHandlerTests
    {
        private static readonly PaginationQuery FirstPage = new(1, 20);

        // A university -> faculty -> department chain, already placed in the fake scope hierarchy.
        private static (UniversityTestEnv Env, Faculty Faculty, Department Department) NewWorld()
        {
            var env = new UniversityTestEnv();
            var university = env.SeedUniversity();
            var faculty = env.SeedFaculty(university.Id);
            var department = env.SeedDepartment(faculty.Id);
            env.PlaceDepartmentInFaculty(department.Id, faculty.Id);
            return (env, faculty, department);
        }

        private static CreateProgramCommand NewCommand(Guid departmentId, string code = "BSC-CSE")
            => new(departmentId, code, "BSc Computer Engineering", DegreeType.Bachelor, 5, 180);

        // ---------- create ----------

        [Fact]
        public async Task Create_succeeds_saves_once_and_writes_an_audit_entry()
        {
            var (env, _, department) = NewWorld();
            env.AsSuperAdmin();

            var result = await env.ProgramHandler.CreateAsync(NewCommand(department.Id), UniversityTestEnv.Request);

            var summary = ResultAssert.Succeeded(result);
            Assert.Equal("BSC-CSE", summary.Code);
            Assert.Equal(department.Id, summary.DepartmentId);
            Assert.True(summary.IsActive);
            Assert.Single(env.Programs.Items);
            Assert.Equal(1, env.Rbac.Uow.SaveCalls);

            var audit = Assert.Single(env.Rbac.Audit.Entries);
            Assert.Equal(UniversityAuditActions.ProgramCreated, audit.Action);
            Assert.Equal(env.ActorId, audit.ActorUserId);
            Assert.Equal(summary.Id.ToString(), audit.EntityId);
        }

        [Fact]
        public async Task Create_by_a_dean_inside_their_own_faculty_succeeds()
        {
            var (env, faculty, department) = NewWorld();
            env.AsDeanOf(faculty.Id);

            var result = await env.ProgramHandler.CreateAsync(NewCommand(department.Id), UniversityTestEnv.Request);

            ResultAssert.Succeeded(result);
        }

        [Fact]
        public async Task Create_by_a_dean_of_another_faculty_is_out_of_scope_and_saves_nothing()
        {
            var (env, _, department) = NewWorld();
            env.AsDeanOf(Guid.NewGuid());

            var result = await env.ProgramHandler.CreateAsync(NewCommand(department.Id), UniversityTestEnv.Request);

            ResultAssert.Failed(result, UniCore.Domain.Modules.Identity.IdentityErrors.ForbiddenOutOfScope);
            Assert.Empty(env.Programs.Items);
            Assert.Equal(0, env.Rbac.Uow.SaveCalls);
            Assert.Empty(env.Rbac.Audit.Entries);
        }

        [Fact]
        public async Task Create_without_the_manage_permission_is_forbidden()
        {
            var (env, _, department) = NewWorld();
            env.AsReader();

            var result = await env.ProgramHandler.CreateAsync(NewCommand(department.Id), UniversityTestEnv.Request);

            ResultAssert.Failed(result, UniCore.Domain.Modules.Identity.IdentityErrors.ForbiddenPermissionMissing);
            Assert.Empty(env.Programs.Items);
        }

        [Fact]
        public async Task Create_when_not_signed_in_is_unauthorized()
        {
            var (env, _, department) = NewWorld();

            var result = await env.ProgramHandler.CreateAsync(NewCommand(department.Id), UniversityTestEnv.Request);

            Assert.Equal(ErrorType.Unauthorized, result.Error?.ErrorType);
        }

        [Fact]
        public async Task Create_with_an_empty_department_id_is_a_validation_error()
        {
            var (env, _, _) = NewWorld();
            env.AsSuperAdmin();

            var result = await env.ProgramHandler.CreateAsync(NewCommand(Guid.Empty), UniversityTestEnv.Request);

            ResultAssert.Failed(result, UniversityErrors.InvalidParent);
        }

        [Fact]
        public async Task Create_with_a_missing_degree_type_is_a_validation_error_not_diploma()
        {
            var (env, _, department) = NewWorld();
            env.AsSuperAdmin();

            var command = new CreateProgramCommand(department.Id, "BSC", "Program", null, 4, 150);
            var result = await env.ProgramHandler.CreateAsync(command, UniversityTestEnv.Request);

            ResultAssert.Failed(result, UniversityErrors.InvalidDegreeType);
            Assert.Empty(env.Programs.Items);
        }

        [Fact]
        public async Task Create_in_an_unknown_department_is_not_found()
        {
            var (env, _, _) = NewWorld();
            env.AsSuperAdmin();

            var result = await env.ProgramHandler.CreateAsync(NewCommand(Guid.NewGuid()), UniversityTestEnv.Request);

            ResultAssert.Failed(result, UniversityErrors.DepartmentNotFound);
        }

        [Fact]
        public async Task Create_in_an_inactive_department_is_rejected()
        {
            var env = new UniversityTestEnv();
            var university = env.SeedUniversity();
            var faculty = env.SeedFaculty(university.Id);
            var department = env.SeedDepartment(faculty.Id, active: false);
            env.AsSuperAdmin();

            var result = await env.ProgramHandler.CreateAsync(NewCommand(department.Id), UniversityTestEnv.Request);

            ResultAssert.Failed(result, UniversityErrors.ParentInactive);
        }

        [Fact]
        public async Task Create_with_invalid_domain_data_returns_the_domain_error()
        {
            var (env, _, department) = NewWorld();
            env.AsSuperAdmin();

            var command = new CreateProgramCommand(department.Id, "BSC", "Program", DegreeType.Bachelor, 99, 150);
            var result = await env.ProgramHandler.CreateAsync(command, UniversityTestEnv.Request);

            ResultAssert.Failed(result, UniversityErrors.InvalidDuration);
        }

        [Fact]
        public async Task Create_with_a_duplicate_code_returns_the_save_error_and_writes_no_audit()
        {
            var (env, _, department) = NewWorld();
            env.AsSuperAdmin();
            env.Rbac.Uow.FailWith = CommonErrors.DuplicateResource;

            var result = await env.ProgramHandler.CreateAsync(NewCommand(department.Id), UniversityTestEnv.Request);

            ResultAssert.Failed(result, CommonErrors.DuplicateResource);
            Assert.Empty(env.Rbac.Audit.Entries);
        }

        // ---------- update ----------

        [Fact]
        public async Task Update_changes_the_program_and_audits_old_and_new_values()
        {
            var (env, _, department) = NewWorld();
            var program = env.SeedProgram(department.Id);
            env.AsSuperAdmin();

            var result = await env.ProgramHandler.UpdateAsync(program.Id, new UpdateProgramCommand("MSc Computer Engineering", DegreeType.Master, 2, 60), UniversityTestEnv.Request);

            var summary = ResultAssert.Succeeded(result);
            Assert.Equal("MSc Computer Engineering", summary.Name);
            Assert.Equal(DegreeType.Master, summary.DegreeType);
            Assert.Equal(UniversityTestEnv.Start, summary.UpdatedAt);

            var audit = Assert.Single(env.Rbac.Audit.Entries);
            Assert.Equal(UniversityAuditActions.ProgramUpdated, audit.Action);
            Assert.Contains("Degree=Bachelor", audit.OldValues);
            Assert.Contains("Degree=Master", audit.NewValues);
        }

        [Fact]
        public async Task Update_of_an_unknown_program_is_not_found()
        {
            var (env, _, _) = NewWorld();
            env.AsSuperAdmin();

            var result = await env.ProgramHandler.UpdateAsync(Guid.NewGuid(), new UpdateProgramCommand("Name", DegreeType.Master, 2, 60), UniversityTestEnv.Request);

            ResultAssert.Failed(result, UniversityErrors.ProgramNotFound);
        }

        [Fact]
        public async Task Update_without_the_permission_does_not_reveal_whether_the_program_exists()
        {
            var (env, _, _) = NewWorld();
            env.AsReader();

            var result = await env.ProgramHandler.UpdateAsync(Guid.NewGuid(), new UpdateProgramCommand("Name", DegreeType.Master, 2, 60), UniversityTestEnv.Request);

            ResultAssert.Failed(result, UniCore.Domain.Modules.Identity.IdentityErrors.ForbiddenPermissionMissing);
        }

        [Fact]
        public async Task Update_by_a_dean_of_another_faculty_is_out_of_scope()
        {
            var (env, _, department) = NewWorld();
            var program = env.SeedProgram(department.Id);
            env.AsDeanOf(Guid.NewGuid());

            var result = await env.ProgramHandler.UpdateAsync(program.Id, new UpdateProgramCommand("Name", DegreeType.Master, 2, 60), UniversityTestEnv.Request);

            ResultAssert.Failed(result, UniCore.Domain.Modules.Identity.IdentityErrors.ForbiddenOutOfScope);
        }

        [Fact]
        public async Task Update_with_a_missing_degree_type_is_a_validation_error()
        {
            var (env, _, department) = NewWorld();
            var program = env.SeedProgram(department.Id);
            env.AsSuperAdmin();

            var result = await env.ProgramHandler.UpdateAsync(program.Id, new UpdateProgramCommand("Name", null, 2, 60), UniversityTestEnv.Request);

            ResultAssert.Failed(result, UniversityErrors.InvalidDegreeType);
        }

        // ---------- activate / deactivate ----------

        [Fact]
        public async Task Deactivate_is_rejected_while_students_study_in_the_program()
        {
            var (env, _, department) = NewWorld();
            var program = env.SeedProgram(department.Id);
            env.Usage.HasStudents = true;
            env.AsSuperAdmin();

            var result = await env.ProgramHandler.DeactivateAsync(program.Id, UniversityTestEnv.Request);

            ResultAssert.Failed(result, UniversityErrors.ProgramHasActiveStudents);
            Assert.True(program.IsActive);
            Assert.Equal(0, env.Rbac.Uow.SaveCalls);
        }

        [Fact]
        public async Task Deactivate_returns_the_lookup_error_instead_of_assuming_no_students()
        {
            var (env, _, department) = NewWorld();
            var program = env.SeedProgram(department.Id);
            env.Usage.FailWith = CommonErrors.DatabaseTimeout;
            env.AsSuperAdmin();

            var result = await env.ProgramHandler.DeactivateAsync(program.Id, UniversityTestEnv.Request);

            ResultAssert.Failed(result, CommonErrors.DatabaseTimeout);
            Assert.True(program.IsActive);
        }

        [Fact]
        public async Task Deactivate_without_students_deactivates_and_audits()
        {
            var (env, _, department) = NewWorld();
            var program = env.SeedProgram(department.Id);
            env.AsSuperAdmin();

            var result = await env.ProgramHandler.DeactivateAsync(program.Id, UniversityTestEnv.Request);

            var summary = ResultAssert.Succeeded(result);
            Assert.False(summary.IsActive);
            Assert.False(program.IsActive);
            Assert.Equal(UniversityAuditActions.ProgramDeactivated, Assert.Single(env.Rbac.Audit.Entries).Action);
        }

        [Fact]
        public async Task Activate_is_rejected_while_the_department_is_inactive()
        {
            var env = new UniversityTestEnv();
            var university = env.SeedUniversity();
            var faculty = env.SeedFaculty(university.Id);
            var department = env.SeedDepartment(faculty.Id);
            var program = env.SeedProgram(department.Id, active: false);
            ResultAssert.Succeeded(department.Deactivate(hasActivePrograms: false));
            env.AsSuperAdmin();

            var result = await env.ProgramHandler.ActivateAsync(program.Id, UniversityTestEnv.Request);

            ResultAssert.Failed(result, UniversityErrors.ParentInactive);
            Assert.False(program.IsActive);
        }

        [Fact]
        public async Task Activate_reactivates_an_inactive_program()
        {
            var (env, _, department) = NewWorld();
            var program = env.SeedProgram(department.Id, active: false);
            env.AsSuperAdmin();

            var result = await env.ProgramHandler.ActivateAsync(program.Id, UniversityTestEnv.Request);

            Assert.True(ResultAssert.Succeeded(result).IsActive);
        }

        // ---------- read ----------

        [Fact]
        public async Task Get_returns_the_program_and_not_found_for_an_unknown_id()
        {
            var (env, _, department) = NewWorld();
            var program = env.SeedProgram(department.Id);
            env.AsReader();

            var found = await env.ProgramHandler.GetAsync(program.Id);
            var missing = await env.ProgramHandler.GetAsync(Guid.NewGuid());

            Assert.Equal(program.Id, ResultAssert.Succeeded(found).Id);
            ResultAssert.Failed(missing, UniversityErrors.ProgramNotFound);
        }

        [Fact]
        public async Task List_filters_by_department_active_flag_and_degree_type()
        {
            var (env, faculty, department) = NewWorld();
            var otherDepartment = env.SeedDepartment(faculty.Id, "MEC");
            env.SeedProgram(department.Id, "BSC-CSE");
            env.SeedProgram(department.Id, "DIP-CSE", active: false);
            env.SeedProgram(otherDepartment.Id, "BSC-MEC");
            env.AsReader();

            var inDepartment = ResultAssert.Succeeded(await env.ProgramHandler.ListAsync(FirstPage, null, null, null, department.Id, null));
            var onlyActive = ResultAssert.Succeeded(await env.ProgramHandler.ListAsync(FirstPage, null, null, true, department.Id, null));
            var masters = ResultAssert.Succeeded(await env.ProgramHandler.ListAsync(FirstPage, null, null, null, null, DegreeType.Master));

            Assert.Equal(2, inDepartment.TotalCount);
            Assert.Equal(1, onlyActive.TotalCount);
            Assert.Equal(0, masters.TotalCount);
        }

        [Fact]
        public async Task List_searches_by_code_prefix_or_name_text()
        {
            var (env, _, department) = NewWorld();
            env.SeedProgram(department.Id, "BSC-CSE");
            env.SeedProgram(department.Id, "DIP-CSE");
            env.AsReader();

            var byCode = ResultAssert.Succeeded(await env.ProgramHandler.ListAsync(FirstPage, "BSC", null, null, null, null));
            var byName = ResultAssert.Succeeded(await env.ProgramHandler.ListAsync(FirstPage, "DIP-CSE", null, null, null, null));

            Assert.Equal("BSC-CSE", Assert.Single(byCode.Items).Code);
            Assert.Equal("DIP-CSE", Assert.Single(byName.Items).Code);
        }

        [Fact]
        public async Task List_pages_the_results_and_reports_the_total()
        {
            var (env, _, department) = NewWorld();
            for (var i = 1; i <= 5; i++)
            {
                env.SeedProgram(department.Id, "P-" + i);
            }
            env.AsReader();

            var page = ResultAssert.Succeeded(await env.ProgramHandler.ListAsync(new PaginationQuery(2, 2), null, null, null, null, null));

            Assert.Equal(2, page.Items.Count);
            Assert.Equal(5, page.TotalCount);
            Assert.Equal(3, page.TotalPages);
            Assert.True(page.HasPrevious);
            Assert.True(page.HasNext);
        }

        [Fact]
        public async Task List_rejects_a_sort_field_outside_the_whitelist()
        {
            var (env, _, _) = NewWorld();
            env.AsReader();

            var result = await env.ProgramHandler.ListAsync(FirstPage, null, "password", null, null, null);

            Assert.Equal("SORT_FIELD_INVALID", result.Error?.Code);
        }

        [Fact]
        public async Task List_requires_the_read_permission()
        {
            var (env, _, _) = NewWorld();

            var result = await env.ProgramHandler.ListAsync(FirstPage, null, null, null, null, null);

            Assert.Equal(ErrorType.Unauthorized, result.Error?.ErrorType);
        }
    }
}
