using UniCore.Application.Common.Abstractions;
using UniCore.Application.Common.Pagination;
using UniCore.Application.Modules.People;
using UniCore.Domain.Modules.Identity;
using UniCore.Domain.Modules.People;
using UniCore.Domain.Modules.University;
using UniCore.UnitTests.Common;

namespace UniCore.UnitTests.Modules.People
{
    public class PeopleHandlersTests
    {
        private static readonly RequestInfo Req = PeopleCalendarTestEnv.Request;

        private static CreatePersonCommand NewPerson(string? nationalId = "29901011234567")
            => new("Nour", "Ahmed", new DateOnly(2000, 5, 1), Gender.Female, nationalId, "nour@uni.edu", null);

        // ---------- person profiles ----------

        [Fact]
        public async Task Creating_a_person_works_and_the_audit_entry_never_contains_the_national_id()
        {
            var env = new PeopleCalendarTestEnv();
            env.AsSuperAdmin();

            var summary = ResultAssert.Succeeded(await env.PersonHandler.CreateAsync(NewPerson(), Req));

            Assert.Equal("29901011234567", summary.NationalId);
            var entry = Assert.Single(env.Rbac.Audit.Entries);
            Assert.Equal(PeopleAuditActions.PersonCreated, entry.Action);
            Assert.DoesNotContain("29901011234567", entry.NewValues);
        }

        [Fact]
        public async Task A_second_person_with_the_same_national_id_is_rejected_even_in_another_case()
        {
            var env = new PeopleCalendarTestEnv();
            env.SeedPerson(nationalId: "AB12345");
            env.AsSuperAdmin();

            var result = await env.PersonHandler.CreateAsync(NewPerson("ab12345"), Req);

            ResultAssert.Failed(result, PeopleErrors.NationalIdExists);
            Assert.Single(env.Persons.Items);
        }

        [Fact]
        public async Task Updating_to_a_national_id_of_someone_else_is_rejected_but_keeping_your_own_is_fine()
        {
            var env = new PeopleCalendarTestEnv();
            var first = env.SeedPerson("A", "One", "AB12345");
            env.SeedPerson("B", "Two", "CD67890");
            env.AsSuperAdmin();

            var clash = await env.PersonHandler.UpdateAsync(first.Id, new UpdatePersonCommand("A", "One", null, null, "CD67890", null, null), Req);
            var same = await env.PersonHandler.UpdateAsync(first.Id, new UpdatePersonCommand("A", "Uno", null, null, "AB12345", null, null), Req);

            ResultAssert.Failed(clash, PeopleErrors.NationalIdExists);
            Assert.Equal("Uno", ResultAssert.Succeeded(same).LastName);
            Assert.Equal("AB12345", first.NationalId);
        }

        [Fact]
        public async Task A_reader_sees_people_without_the_national_id_and_cannot_change_them()
        {
            var env = new PeopleCalendarTestEnv();
            var person = env.SeedPerson(nationalId: "AB12345");
            env.AsPeopleReader();

            var one = ResultAssert.Succeeded(await env.PersonHandler.GetAsync(person.Id));
            var page = ResultAssert.Succeeded(await env.PersonHandler.ListAsync(new PaginationQuery(1, 20), null, null));
            var create = await env.PersonHandler.CreateAsync(NewPerson("ZZ99999"), Req);

            Assert.Null(one.NationalId);
            Assert.All(page.Items, p => Assert.Null(p.NationalId));
            Assert.True(create.IsFailure);
            Assert.Single(env.Persons.Items);
        }

        [Fact]
        public async Task A_manager_sees_the_national_id()
        {
            var env = new PeopleCalendarTestEnv();
            var person = env.SeedPerson(nationalId: "AB12345");
            env.AsSuperAdmin();

            Assert.Equal("AB12345", ResultAssert.Succeeded(await env.PersonHandler.GetAsync(person.Id)).NationalId);
        }

        [Fact]
        public async Task Searching_people_matches_names_and_ignores_the_national_id()
        {
            var env = new PeopleCalendarTestEnv();
            env.SeedPerson("Nour", "Ahmed", "AB12345");
            env.SeedPerson("Sara", "Ali", "CD67890");
            env.AsSuperAdmin();

            var byName = ResultAssert.Succeeded(await env.PersonHandler.ListAsync(new PaginationQuery(1, 20), "Sar", null));
            var byNationalId = ResultAssert.Succeeded(await env.PersonHandler.ListAsync(new PaginationQuery(1, 20), "AB12345", null));

            Assert.Equal("Sara", Assert.Single(byName.Items).FirstName);
            Assert.Empty(byNationalId.Items);
        }

        [Fact]
        public async Task Linking_a_user_works_once_per_person_and_once_per_user()
        {
            var env = new PeopleCalendarTestEnv();
            var first = env.SeedPerson("A", "One");
            var second = env.SeedPerson("B", "Two");
            var userId = Guid.NewGuid();
            env.AsSuperAdmin();

            ResultAssert.Succeeded(await env.PersonHandler.LinkUserAsync(first.Id, userId, Req));
            var sameUserAgain = await env.PersonHandler.LinkUserAsync(second.Id, userId, Req);
            var otherUserOnLinkedPerson = await env.PersonHandler.LinkUserAsync(first.Id, Guid.NewGuid(), Req);

            ResultAssert.Failed(sameUserAgain, PeopleErrors.UserAlreadyLinked);
            ResultAssert.Failed(otherUserOnLinkedPerson, PeopleErrors.PersonAlreadyLinked);
            Assert.Null(second.UserId);
            Assert.Contains(env.Rbac.Audit.Entries, e => e.Action == PeopleAuditActions.PersonUserLinked);
        }

        [Fact]
        public async Task Linking_an_empty_user_id_is_a_validation_error()
        {
            var env = new PeopleCalendarTestEnv();
            var person = env.SeedPerson();
            env.AsSuperAdmin();

            ResultAssert.Failed(await env.PersonHandler.LinkUserAsync(person.Id, Guid.Empty, Req), PeopleErrors.InvalidUser);
        }

        // ---------- employees ----------

        [Fact]
        public async Task Creating_an_employee_works_in_the_deans_own_faculty_only()
        {
            var env = new PeopleCalendarTestEnv();
            var own = env.SeedFaculty("ENG");
            var other = env.SeedFaculty("SCI");
            var ownDepartment = env.SeedDepartment(own.Id, "CSE");
            var otherDepartment = env.SeedDepartment(other.Id, "PHY");
            env.PlaceDepartmentInFaculty(ownDepartment.Id, own.Id);
            env.PlaceDepartmentInFaculty(otherDepartment.Id, other.Id);
            var a = env.SeedPerson("A", "One");
            var b = env.SeedPerson("B", "Two");
            env.AsDeanOf(own.Id);

            var allowed = await env.EmployeeHandler.CreateAsync(new CreateEmployeeCommand(a.Id, "emp-1", ownDepartment.Id, "Lecturer", new DateOnly(2026, 9, 1)), Req);
            var denied = await env.EmployeeHandler.CreateAsync(new CreateEmployeeCommand(b.Id, "emp-2", otherDepartment.Id, null, new DateOnly(2026, 9, 1)), Req);
            var noDepartment = await env.EmployeeHandler.CreateAsync(new CreateEmployeeCommand(b.Id, "emp-3", null, null, new DateOnly(2026, 9, 1)), Req);

            Assert.Equal("EMP-1", ResultAssert.Succeeded(allowed).EmployeeNumber);
            ResultAssert.Failed(denied, IdentityErrors.ForbiddenOutOfScope);
            ResultAssert.Failed(noDepartment, IdentityErrors.ForbiddenOutOfScope);
            Assert.Single(env.Employees.Items);
        }

        [Fact]
        public async Task An_employee_needs_an_existing_person_and_an_active_department()
        {
            var env = new PeopleCalendarTestEnv();
            var faculty = env.SeedFaculty();
            var inactive = env.SeedDepartment(faculty.Id, "OLD", active: false);
            var person = env.SeedPerson();
            env.AsSuperAdmin();
            var hired = new DateOnly(2026, 9, 1);

            var noPerson = await env.EmployeeHandler.CreateAsync(new CreateEmployeeCommand(Guid.NewGuid(), "EMP-1", null, null, hired), Req);
            var noDepartment = await env.EmployeeHandler.CreateAsync(new CreateEmployeeCommand(person.Id, "EMP-1", Guid.NewGuid(), null, hired), Req);
            var inactiveDepartment = await env.EmployeeHandler.CreateAsync(new CreateEmployeeCommand(person.Id, "EMP-1", inactive.Id, null, hired), Req);

            ResultAssert.Failed(noPerson, PeopleErrors.PersonNotFound);
            ResultAssert.Failed(noDepartment, UniversityErrors.DepartmentNotFound);
            ResultAssert.Failed(inactiveDepartment, UniversityErrors.ParentInactive);
        }

        [Fact]
        public async Task Employee_numbers_are_unique_and_a_person_is_an_employee_once()
        {
            var env = new PeopleCalendarTestEnv();
            var a = env.SeedPerson("A", "One");
            var b = env.SeedPerson("B", "Two");
            env.SeedEmployee(a.Id, "EMP-001");
            env.AsSuperAdmin();
            var hired = new DateOnly(2026, 9, 1);

            var sameNumber = await env.EmployeeHandler.CreateAsync(new CreateEmployeeCommand(b.Id, "emp-001", null, null, hired), Req);
            var samePerson = await env.EmployeeHandler.CreateAsync(new CreateEmployeeCommand(a.Id, "EMP-002", null, null, hired), Req);

            ResultAssert.Failed(sameNumber, PeopleErrors.EmployeeNumberExists);
            ResultAssert.Failed(samePerson, PeopleErrors.EmployeeAlreadyExists);
        }

        [Fact]
        public async Task Moving_an_employee_to_another_faculty_needs_authority_over_both()
        {
            var env = new PeopleCalendarTestEnv();
            var own = env.SeedFaculty("ENG");
            var other = env.SeedFaculty("SCI");
            var ownDepartment = env.SeedDepartment(own.Id, "CSE");
            var otherDepartment = env.SeedDepartment(other.Id, "PHY");
            env.PlaceDepartmentInFaculty(ownDepartment.Id, own.Id);
            env.PlaceDepartmentInFaculty(otherDepartment.Id, other.Id);
            var employee = env.SeedEmployee(env.SeedPerson().Id, "EMP-001", ownDepartment.Id);
            env.AsDeanOf(own.Id);

            var result = await env.EmployeeHandler.UpdateAsync(employee.Id, new UpdateEmployeeCommand(otherDepartment.Id, "Moved"), Req);

            ResultAssert.Failed(result, IdentityErrors.ForbiddenOutOfScope);
            Assert.Equal(ownDepartment.Id, employee.DepartmentId);
        }

        [Fact]
        public async Task Terminating_an_employee_audits_it_and_a_second_termination_is_rejected()
        {
            var env = new PeopleCalendarTestEnv();
            var employee = env.SeedEmployee(env.SeedPerson().Id);
            env.AsSuperAdmin();

            var terminated = ResultAssert.Succeeded(await env.EmployeeHandler.TerminateAsync(employee.Id, new DateOnly(2026, 12, 31), Req));
            var again = await env.EmployeeHandler.TerminateAsync(employee.Id, new DateOnly(2027, 1, 31), Req);

            Assert.Equal(EmployeeStatus.Terminated, terminated.Status);
            ResultAssert.Failed(again, PeopleErrors.EmployeeAlreadyTerminated);
            Assert.Equal(PeopleAuditActions.EmployeeTerminated, Assert.Single(env.Rbac.Audit.Entries).Action);
            Assert.Single(employee.DomainEvents);
        }

        [Fact]
        public async Task A_caller_without_the_permission_cannot_tell_whether_an_employee_exists()
        {
            var env = new PeopleCalendarTestEnv();
            var employee = env.SeedEmployee(env.SeedPerson().Id);
            env.AsPeopleReader();

            var known = await env.EmployeeHandler.TerminateAsync(employee.Id, new DateOnly(2026, 12, 31), Req);
            var unknown = await env.EmployeeHandler.TerminateAsync(Guid.NewGuid(), new DateOnly(2026, 12, 31), Req);

            Assert.Equal(known.Error?.Code, unknown.Error?.Code);
            Assert.Equal(EmployeeStatus.Active, employee.Status);
        }

        // ---------- instructors ----------

        [Fact]
        public async Task Creating_an_instructor_needs_an_active_employee_and_only_one_per_employee()
        {
            var env = new PeopleCalendarTestEnv();
            var active = env.SeedEmployee(env.SeedPerson("A", "One").Id, "EMP-001");
            var gone = env.SeedEmployee(env.SeedPerson("B", "Two").Id, "EMP-002");
            ResultAssert.Succeeded(gone.Terminate(new DateOnly(2026, 9, 30), env.Clock.UtcNow));
            env.AsSuperAdmin();

            var first = await env.InstructorHandler.CreateAsync(new CreateInstructorCommand(active.Id, AcademicRank.Lecturer, null), Req);
            var second = await env.InstructorHandler.CreateAsync(new CreateInstructorCommand(active.Id, AcademicRank.Professor, null), Req);
            var terminated = await env.InstructorHandler.CreateAsync(new CreateInstructorCommand(gone.Id, AcademicRank.Lecturer, null), Req);
            var unknown = await env.InstructorHandler.CreateAsync(new CreateInstructorCommand(Guid.NewGuid(), AcademicRank.Lecturer, null), Req);
            var noRank = await env.InstructorHandler.CreateAsync(new CreateInstructorCommand(active.Id, null, null), Req);

            ResultAssert.Succeeded(first);
            ResultAssert.Failed(second, PeopleErrors.InstructorExists);
            ResultAssert.Failed(terminated, PeopleErrors.EmployeeNotActive);
            ResultAssert.Failed(unknown, PeopleErrors.EmployeeNotFound);
            ResultAssert.Failed(noRank, PeopleErrors.InvalidAcademicRank);
            Assert.Single(env.Instructors.Items);
        }

        [Fact]
        public async Task A_dean_manages_instructors_of_their_own_faculty_only()
        {
            var env = new PeopleCalendarTestEnv();
            var own = env.SeedFaculty("ENG");
            var other = env.SeedFaculty("SCI");
            var ownDepartment = env.SeedDepartment(own.Id, "CSE");
            var otherDepartment = env.SeedDepartment(other.Id, "PHY");
            env.PlaceDepartmentInFaculty(ownDepartment.Id, own.Id);
            env.PlaceDepartmentInFaculty(otherDepartment.Id, other.Id);
            var ownEmployee = env.SeedEmployee(env.SeedPerson("A", "One").Id, "EMP-001", ownDepartment.Id);
            var otherEmployee = env.SeedEmployee(env.SeedPerson("B", "Two").Id, "EMP-002", otherDepartment.Id);
            var otherInstructor = env.SeedInstructor(otherEmployee.Id);
            env.AsDeanOf(own.Id);

            var allowed = await env.InstructorHandler.CreateAsync(new CreateInstructorCommand(ownEmployee.Id, AcademicRank.Lecturer, null), Req);
            var denied = await env.InstructorHandler.CreateAsync(new CreateInstructorCommand(otherEmployee.Id, AcademicRank.Lecturer, null), Req);
            var deniedUpdate = await env.InstructorHandler.DeactivateAsync(otherInstructor.Id, Req);

            ResultAssert.Succeeded(allowed);
            ResultAssert.Failed(denied, IdentityErrors.ForbiddenOutOfScope);
            ResultAssert.Failed(deniedUpdate, IdentityErrors.ForbiddenOutOfScope);
            Assert.True(otherInstructor.IsActive);
        }

        [Fact]
        public async Task An_instructor_of_a_terminated_employee_cannot_be_activated_again()
        {
            var env = new PeopleCalendarTestEnv();
            var employee = env.SeedEmployee(env.SeedPerson().Id);
            var instructor = env.SeedInstructor(employee.Id, active: false);
            ResultAssert.Succeeded(employee.Terminate(new DateOnly(2026, 9, 30), env.Clock.UtcNow));
            env.AsSuperAdmin();

            var result = await env.InstructorHandler.ActivateAsync(instructor.Id, Req);

            ResultAssert.Failed(result, PeopleErrors.EmployeeNotActive);
            Assert.False(instructor.IsActive);
        }

        [Fact]
        public async Task Instructors_can_be_filtered_by_rank()
        {
            var env = new PeopleCalendarTestEnv();
            var a = env.SeedInstructor(env.SeedEmployee(env.SeedPerson("A", "One").Id, "EMP-001").Id);
            var b = env.SeedEmployee(env.SeedPerson("B", "Two").Id, "EMP-002");
            env.AsSuperAdmin();
            ResultAssert.Succeeded(await env.InstructorHandler.CreateAsync(new CreateInstructorCommand(b.Id, AcademicRank.Professor, null), Req));

            var professors = ResultAssert.Succeeded(await env.InstructorHandler.ListAsync(new PaginationQuery(1, 20), null, null, AcademicRank.Professor, null));

            Assert.Equal(1, professors.TotalCount);
            Assert.NotEqual(a.Id, professors.Items[0].Id);
        }

        // ---------- teaching assistants ----------

        [Fact]
        public async Task A_teaching_assistant_needs_an_employee_or_a_student()
        {
            var env = new PeopleCalendarTestEnv();
            env.AsSuperAdmin();

            var result = await env.AssistantHandler.CreateAsync(new CreateTeachingAssistantCommand(null, null), Req);

            ResultAssert.Failed(result, PeopleErrors.TaRequiresEmployeeOrStudent);
            Assert.Empty(env.Assistants.Items);
        }

        [Fact]
        public async Task A_graduate_student_can_be_a_teaching_assistant_once()
        {
            var env = new PeopleCalendarTestEnv();
            var student = env.SeedStudent(env.SeedPerson().Id);
            env.AsSuperAdmin();

            var first = await env.AssistantHandler.CreateAsync(new CreateTeachingAssistantCommand(null, student.Id), Req);
            var second = await env.AssistantHandler.CreateAsync(new CreateTeachingAssistantCommand(null, student.Id), Req);
            var unknown = await env.AssistantHandler.CreateAsync(new CreateTeachingAssistantCommand(null, Guid.NewGuid()), Req);

            Assert.Equal(student.Id, ResultAssert.Succeeded(first).StudentId);
            ResultAssert.Failed(second, PeopleErrors.TeachingAssistantExists);
            ResultAssert.Failed(unknown, PeopleErrors.StudentNotFound);
        }

        [Fact]
        public async Task An_employee_assistant_must_exist_and_be_active()
        {
            var env = new PeopleCalendarTestEnv();
            var employee = env.SeedEmployee(env.SeedPerson().Id);
            var gone = env.SeedEmployee(env.SeedPerson("B", "Two").Id, "EMP-002");
            ResultAssert.Succeeded(gone.Terminate(new DateOnly(2026, 9, 30), env.Clock.UtcNow));
            env.AsSuperAdmin();

            ResultAssert.Succeeded(await env.AssistantHandler.CreateAsync(new CreateTeachingAssistantCommand(employee.Id, null), Req));
            ResultAssert.Failed(await env.AssistantHandler.CreateAsync(new CreateTeachingAssistantCommand(gone.Id, null), Req), PeopleErrors.EmployeeNotActive);
            ResultAssert.Failed(await env.AssistantHandler.CreateAsync(new CreateTeachingAssistantCommand(Guid.NewGuid(), null), Req), PeopleErrors.EmployeeNotFound);
        }

        [Fact]
        public async Task A_student_only_assistant_needs_the_top_level_scope()
        {
            var env = new PeopleCalendarTestEnv();
            var student = env.SeedStudent(env.SeedPerson().Id);
            env.AsDeanOf(Guid.NewGuid());

            var result = await env.AssistantHandler.CreateAsync(new CreateTeachingAssistantCommand(null, student.Id), Req);

            ResultAssert.Failed(result, IdentityErrors.ForbiddenOutOfScope);
            Assert.Empty(env.Assistants.Items);
        }
    }

    public class ScopeDataProviderTests
    {
        [Fact]
        public async Task A_user_without_a_person_has_no_scope_data()
        {
            var env = new PeopleCalendarTestEnv();

            var result = await env.ScopeData.GetByUserIdAsync(Guid.NewGuid());

            ResultAssert.Failed(result, PeopleErrors.PersonNotFound);
        }

        [Fact]
        public async Task A_student_gets_the_student_id_and_nothing_else()
        {
            var env = new PeopleCalendarTestEnv();
            var userId = Guid.NewGuid();
            var person = env.SeedPerson(userId: userId);
            var student = env.SeedStudent(person.Id);

            var data = ResultAssert.Succeeded(await env.ScopeData.GetByUserIdAsync(userId));

            Assert.Equal(person.Id, data.PersonProfileId);
            Assert.Equal(student.Id, data.StudentId);
            Assert.Null(data.EmployeeId);
            Assert.Null(data.InstructorId);
            Assert.Null(data.DepartmentId);
            Assert.Empty(data.SectionIds);
        }

        [Fact]
        public async Task An_instructor_gets_department_faculty_and_assigned_sections()
        {
            var env = new PeopleCalendarTestEnv();
            var faculty = env.SeedFaculty();
            var department = env.SeedDepartment(faculty.Id);
            var userId = Guid.NewGuid();
            var employee = env.SeedEmployee(env.SeedPerson(userId: userId).Id, "EMP-001", department.Id);
            var instructor = env.SeedInstructor(employee.Id);
            var section = Guid.NewGuid();
            env.SectionReader.Sections.Add(section);

            var data = ResultAssert.Succeeded(await env.ScopeData.GetByUserIdAsync(userId));

            Assert.Equal(employee.Id, data.EmployeeId);
            Assert.Equal(instructor.Id, data.InstructorId);
            Assert.Equal(department.Id, data.DepartmentId);
            Assert.Equal(faculty.Id, data.FacultyId);
            Assert.Equal(section, Assert.Single(data.SectionIds));
            Assert.Equal(instructor.Id, env.SectionReader.LastInstructorId);
        }

        [Fact]
        public async Task An_inactive_instructor_is_not_offered_to_the_section_reader()
        {
            var env = new PeopleCalendarTestEnv();
            var userId = Guid.NewGuid();
            var employee = env.SeedEmployee(env.SeedPerson(userId: userId).Id);
            env.SeedInstructor(employee.Id, active: false);

            ResultAssert.Succeeded(await env.ScopeData.GetByUserIdAsync(userId));

            Assert.Null(env.SectionReader.LastInstructorId);
        }

        [Fact]
        public async Task A_terminated_employee_gets_no_academic_scope()
        {
            var env = new PeopleCalendarTestEnv();
            var faculty = env.SeedFaculty();
            var department = env.SeedDepartment(faculty.Id);
            var userId = Guid.NewGuid();
            var employee = env.SeedEmployee(env.SeedPerson(userId: userId).Id, "EMP-001", department.Id);
            env.SeedInstructor(employee.Id);
            ResultAssert.Succeeded(employee.Terminate(new DateOnly(2026, 9, 30), env.Clock.UtcNow));

            var data = ResultAssert.Succeeded(await env.ScopeData.GetByUserIdAsync(userId));

            Assert.Null(data.InstructorId);
            Assert.Null(data.DepartmentId);
            Assert.Null(data.FacultyId);
        }

        [Fact]
        public async Task A_graduate_student_assistant_is_found_through_the_student_record()
        {
            var env = new PeopleCalendarTestEnv();
            var userId = Guid.NewGuid();
            var person = env.SeedPerson(userId: userId);
            var student = env.SeedStudent(person.Id);
            env.AsSuperAdmin();
            var created = ResultAssert.Succeeded(await env.AssistantHandler.CreateAsync(new CreateTeachingAssistantCommand(null, student.Id), PeopleCalendarTestEnv.Request));

            var data = ResultAssert.Succeeded(await env.ScopeData.GetByUserIdAsync(userId));

            Assert.Equal(created.Id, data.TeachingAssistantId);
            Assert.Equal(created.Id, env.SectionReader.LastAssistantId);
        }
    }
}
