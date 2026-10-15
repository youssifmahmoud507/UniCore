using UniCore.Application.Modules.University;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Identity;
using UniCore.Domain.Modules.University;
using UniCore.UnitTests.Common;

namespace UniCore.UnitTests.Modules.University
{
    // University, campus, faculty and department handlers: the rules around parents and children.
    // (Programs have their own file.)
    public class UniversityStructureHandlersTests
    {
        private static CreateCampusCommand NewCampus(Guid universityId, string timeZone = "UTC")
            => new(universityId, "MAIN", "Main Campus", new AddressInput("1 Gamaa St", "Giza", null, null, "Egypt"), timeZone);

        // ---------- university ----------

        [Fact]
        public async Task Creating_a_university_succeeds_for_a_super_admin_and_is_audited()
        {
            var env = new UniversityTestEnv();
            env.AsSuperAdmin();

            var result = await env.UniversityHandler.CreateAsync(
                new CreateUniversityCommand("cu", "Cairo University", null, null, null, null), UniversityTestEnv.Request);

            var summary = ResultAssert.Succeeded(result);
            Assert.Equal("CU", summary.Code);
            Assert.Single(env.Universities.Items);
            Assert.Equal(UniversityAuditActions.UniversityCreated, Assert.Single(env.Rbac.Audit.Entries).Action);
        }

        [Fact]
        public async Task Creating_a_university_needs_the_top_level_scope()
        {
            var env = new UniversityTestEnv();
            env.AsDeanOf(Guid.NewGuid());

            var result = await env.UniversityHandler.CreateAsync(
                new CreateUniversityCommand("CU", "Cairo University", null, null, null, null), UniversityTestEnv.Request);

            ResultAssert.Failed(result, IdentityErrors.ForbiddenOutOfScope);
            Assert.Empty(env.Universities.Items);
        }

        [Fact]
        public async Task Deactivating_a_university_with_an_active_faculty_is_rejected()
        {
            var env = new UniversityTestEnv();
            var university = env.SeedUniversity();
            env.SeedFaculty(university.Id);
            env.AsSuperAdmin();

            var result = await env.UniversityHandler.DeactivateAsync(university.Id, UniversityTestEnv.Request);

            ResultAssert.Failed(result, UniversityErrors.UniversityHasActiveChildren);
            Assert.True(university.IsActive);
        }

        [Fact]
        public async Task Deactivating_a_university_with_an_active_campus_is_rejected()
        {
            var env = new UniversityTestEnv();
            var university = env.SeedUniversity();
            env.AsSuperAdmin();
            ResultAssert.Succeeded(await env.CampusHandler.CreateAsync(NewCampus(university.Id), UniversityTestEnv.Request));

            var result = await env.UniversityHandler.DeactivateAsync(university.Id, UniversityTestEnv.Request);

            ResultAssert.Failed(result, UniversityErrors.UniversityHasActiveChildren);
        }

        [Fact]
        public async Task Deactivating_a_university_without_active_children_works()
        {
            var env = new UniversityTestEnv();
            var university = env.SeedUniversity();
            var faculty = env.SeedFaculty(university.Id, active: false);
            Assert.False(faculty.IsActive);
            env.AsSuperAdmin();

            var result = await env.UniversityHandler.DeactivateAsync(university.Id, UniversityTestEnv.Request);

            Assert.False(ResultAssert.Succeeded(result).IsActive);
        }

        [Fact]
        public async Task Getting_an_unknown_university_is_not_found()
        {
            var env = new UniversityTestEnv();
            env.AsReader();

            ResultAssert.Failed(await env.UniversityHandler.GetAsync(Guid.NewGuid()), UniversityErrors.UniversityNotFound);
        }

        // ---------- campus ----------

        [Fact]
        public async Task Creating_a_campus_with_a_known_time_zone_succeeds()
        {
            var env = new UniversityTestEnv();
            var university = env.SeedUniversity();
            env.AsSuperAdmin();

            var result = await env.CampusHandler.CreateAsync(NewCampus(university.Id), UniversityTestEnv.Request);

            var summary = ResultAssert.Succeeded(result);
            Assert.Equal("MAIN", summary.Code);
            Assert.Equal("Giza", summary.Address.City);
            Assert.Equal("UTC", summary.TimeZone);
        }

        [Fact]
        public async Task Creating_a_campus_with_a_time_zone_the_server_does_not_know_is_rejected()
        {
            var env = new UniversityTestEnv();
            var university = env.SeedUniversity();
            env.AsSuperAdmin();

            // The format is valid, so the domain would accept it; the application layer checks that it exists.
            var result = await env.CampusHandler.CreateAsync(NewCampus(university.Id, "Mars/Olympus"), UniversityTestEnv.Request);

            ResultAssert.Failed(result, UniversityErrors.TimeZoneUnknown);
            Assert.Empty(env.Campuses.Items);
        }

        [Fact]
        public async Task Creating_a_campus_without_an_address_is_rejected()
        {
            var env = new UniversityTestEnv();
            var university = env.SeedUniversity();
            env.AsSuperAdmin();

            var result = await env.CampusHandler.CreateAsync(
                new CreateCampusCommand(university.Id, "MAIN", "Main Campus", null, "UTC"), UniversityTestEnv.Request);

            ResultAssert.Failed(result, UniversityErrors.AddressRequired);
        }

        [Fact]
        public async Task Creating_a_campus_under_an_inactive_university_is_rejected()
        {
            var env = new UniversityTestEnv();
            var university = env.SeedUniversity(active: false);
            env.AsSuperAdmin();

            ResultAssert.Failed(await env.CampusHandler.CreateAsync(NewCampus(university.Id), UniversityTestEnv.Request), UniversityErrors.ParentInactive);
        }

        // ---------- faculty ----------

        [Fact]
        public async Task Creating_a_faculty_succeeds_under_an_active_university()
        {
            var env = new UniversityTestEnv();
            var university = env.SeedUniversity();
            env.AsSuperAdmin();

            var result = await env.FacultyHandler.CreateAsync(
                new CreateFacultyCommand(university.Id, "eng", "Faculty of Engineering", null, null, null), UniversityTestEnv.Request);

            var summary = ResultAssert.Succeeded(result);
            Assert.Equal("ENG", summary.Code);
            Assert.Equal(university.Id, summary.UniversityId);
            Assert.Equal(UniversityAuditActions.FacultyCreated, Assert.Single(env.Rbac.Audit.Entries).Action);
        }

        [Fact]
        public async Task Creating_a_faculty_under_an_unknown_or_inactive_university_is_rejected()
        {
            var env = new UniversityTestEnv();
            var inactive = env.SeedUniversity(active: false);
            env.AsSuperAdmin();

            var unknown = await env.FacultyHandler.CreateAsync(
                new CreateFacultyCommand(Guid.NewGuid(), "ENG", "Faculty of Engineering", null, null, null), UniversityTestEnv.Request);
            var notActive = await env.FacultyHandler.CreateAsync(
                new CreateFacultyCommand(inactive.Id, "ENG", "Faculty of Engineering", null, null, null), UniversityTestEnv.Request);

            ResultAssert.Failed(unknown, UniversityErrors.UniversityNotFound);
            ResultAssert.Failed(notActive, UniversityErrors.ParentInactive);
        }

        [Fact]
        public async Task A_dean_cannot_create_a_faculty_but_can_update_their_own()
        {
            var env = new UniversityTestEnv();
            var university = env.SeedUniversity();
            var faculty = env.SeedFaculty(university.Id);
            env.AsDeanOf(faculty.Id);

            var create = await env.FacultyHandler.CreateAsync(
                new CreateFacultyCommand(university.Id, "SCI", "Faculty of Science", null, null, null), UniversityTestEnv.Request);
            var update = await env.FacultyHandler.UpdateAsync(
                faculty.Id, new UpdateFacultyCommand("Engineering", null, null, null), UniversityTestEnv.Request);

            ResultAssert.Failed(create, IdentityErrors.ForbiddenOutOfScope);
            Assert.Equal("Engineering", ResultAssert.Succeeded(update).Name);
        }

        [Fact]
        public async Task A_dean_cannot_update_another_faculty()
        {
            var env = new UniversityTestEnv();
            var university = env.SeedUniversity();
            var faculty = env.SeedFaculty(university.Id, "ENG");
            var other = env.SeedFaculty(university.Id, "SCI");
            env.AsDeanOf(faculty.Id);

            var result = await env.FacultyHandler.UpdateAsync(
                other.Id, new UpdateFacultyCommand("Hacked", null, null, null), UniversityTestEnv.Request);

            ResultAssert.Failed(result, IdentityErrors.ForbiddenOutOfScope);
            Assert.Equal("Faculty SCI", other.Name);
        }

        [Fact]
        public async Task Deactivating_a_faculty_with_an_active_department_is_rejected()
        {
            var env = new UniversityTestEnv();
            var university = env.SeedUniversity();
            var faculty = env.SeedFaculty(university.Id);
            env.SeedDepartment(faculty.Id);
            env.AsSuperAdmin();

            var result = await env.FacultyHandler.DeactivateAsync(faculty.Id, UniversityTestEnv.Request);

            ResultAssert.Failed(result, UniversityErrors.FacultyHasActiveDepartments);
            Assert.True(faculty.IsActive);
        }

        [Fact]
        public async Task Deactivating_a_faculty_whose_departments_are_inactive_works()
        {
            var env = new UniversityTestEnv();
            var university = env.SeedUniversity();
            var faculty = env.SeedFaculty(university.Id);
            env.SeedDepartment(faculty.Id, active: false);
            env.AsSuperAdmin();

            var result = await env.FacultyHandler.DeactivateAsync(faculty.Id, UniversityTestEnv.Request);

            Assert.False(ResultAssert.Succeeded(result).IsActive);
        }

        // ---------- department ----------

        [Fact]
        public async Task A_dean_can_create_a_department_in_their_faculty_but_not_in_another()
        {
            var env = new UniversityTestEnv();
            var university = env.SeedUniversity();
            var own = env.SeedFaculty(university.Id, "ENG");
            var other = env.SeedFaculty(university.Id, "SCI");
            env.AsDeanOf(own.Id);

            var allowed = await env.DepartmentHandler.CreateAsync(
                new CreateDepartmentCommand(own.Id, "cse", "Computer Engineering", null, null, null), UniversityTestEnv.Request);
            var denied = await env.DepartmentHandler.CreateAsync(
                new CreateDepartmentCommand(other.Id, "phy", "Physics", null, null, null), UniversityTestEnv.Request);

            Assert.Equal("CSE", ResultAssert.Succeeded(allowed).Code);
            ResultAssert.Failed(denied, IdentityErrors.ForbiddenOutOfScope);
            Assert.Single(env.Departments.Items);
        }

        [Fact]
        public async Task Creating_a_department_under_an_inactive_faculty_is_rejected()
        {
            var env = new UniversityTestEnv();
            var university = env.SeedUniversity();
            var faculty = env.SeedFaculty(university.Id, active: false);
            env.AsSuperAdmin();

            var result = await env.DepartmentHandler.CreateAsync(
                new CreateDepartmentCommand(faculty.Id, "CSE", "Computer Engineering", null, null, null), UniversityTestEnv.Request);

            ResultAssert.Failed(result, UniversityErrors.ParentInactive);
        }

        [Fact]
        public async Task Deactivating_a_department_with_an_active_program_is_rejected()
        {
            var env = new UniversityTestEnv();
            var university = env.SeedUniversity();
            var faculty = env.SeedFaculty(university.Id);
            var department = env.SeedDepartment(faculty.Id);
            env.SeedProgram(department.Id);
            env.AsSuperAdmin();

            var result = await env.DepartmentHandler.DeactivateAsync(department.Id, UniversityTestEnv.Request);

            ResultAssert.Failed(result, UniversityErrors.DepartmentHasActivePrograms);
            Assert.True(department.IsActive);
        }

        [Fact]
        public async Task A_dean_can_deactivate_a_department_of_their_faculty_through_the_hierarchy()
        {
            var env = new UniversityTestEnv();
            var university = env.SeedUniversity();
            var faculty = env.SeedFaculty(university.Id);
            var department = env.SeedDepartment(faculty.Id);
            env.PlaceDepartmentInFaculty(department.Id, faculty.Id);
            env.AsDeanOf(faculty.Id);

            var result = await env.DepartmentHandler.DeactivateAsync(department.Id, UniversityTestEnv.Request);

            Assert.False(ResultAssert.Succeeded(result).IsActive);
        }

        [Fact]
        public async Task Listing_departments_by_faculty_returns_only_that_faculty()
        {
            var env = new UniversityTestEnv();
            var university = env.SeedUniversity();
            var first = env.SeedFaculty(university.Id, "ENG");
            var second = env.SeedFaculty(university.Id, "SCI");
            env.SeedDepartment(first.Id, "CSE");
            env.SeedDepartment(first.Id, "MEC");
            env.SeedDepartment(second.Id, "PHY");
            env.AsReader();

            var page = ResultAssert.Succeeded(await env.DepartmentHandler.ListAsync(new(1, 20), null, null, null, first.Id));

            Assert.Equal(2, page.TotalCount);
            Assert.All(page.Items, d => Assert.Equal(first.Id, d.FacultyId));
        }
    }
}
