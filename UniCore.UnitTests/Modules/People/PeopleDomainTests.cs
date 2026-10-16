using UniCore.Domain.Modules.People;
using UniCore.UnitTests.Common;

namespace UniCore.UnitTests.Modules.People
{
    public class PersonProfileTests
    {
        private static readonly DateTimeOffset Now = new(2026, 10, 12, 9, 0, 0, TimeSpan.Zero);
        private static readonly DateOnly Today = new(2026, 10, 12);

        private static PersonProfile NewPerson() => ResultAssert.Succeeded(
            PersonProfile.Create(" Nour ", " Ahmed ", new DateOnly(2000, 5, 1), Gender.Female, "29901011234567", "nour@uni.edu", "+20 100 123 4567", Today, Now));

        [Fact]
        public void Create_trims_names_and_keeps_the_optional_data()
        {
            var person = NewPerson();

            Assert.NotEqual(Guid.Empty, person.Id);
            Assert.Equal("Nour", person.FirstName);
            Assert.Equal("Ahmed", person.LastName);
            Assert.Equal("Nour Ahmed", person.FullName);
            Assert.Equal("29901011234567", person.NationalId);
            Assert.Null(person.UserId);
        }

        [Fact]
        public void Create_needs_only_the_two_names()
        {
            var person = ResultAssert.Succeeded(PersonProfile.Create("A", "B", null, null, null, null, null, Today, Now));

            Assert.Null(person.NationalId);
            Assert.Null(person.Email);
            Assert.Null(person.DateOfBirth);
        }

        [Theory]
        [InlineData(null, "B")]
        [InlineData("A", null)]
        [InlineData(" ", "B")]
        public void Create_rejects_missing_names(string? first, string? last)
        {
            ResultAssert.Failed(PersonProfile.Create(first, last, null, null, null, null, null, Today, Now), PeopleErrors.InvalidName);
        }

        [Fact]
        public void Create_rejects_names_longer_than_100_characters()
        {
            ResultAssert.Failed(PersonProfile.Create(new string('a', 101), "B", null, null, null, null, null, Today, Now), PeopleErrors.InvalidName);
        }

        [Fact]
        public void Create_rejects_a_date_of_birth_that_is_today_or_later()
        {
            ResultAssert.Failed(PersonProfile.Create("A", "B", Today, null, null, null, null, Today, Now), PeopleErrors.InvalidDateOfBirth);
            ResultAssert.Failed(PersonProfile.Create("A", "B", Today.AddDays(1), null, null, null, null, Today, Now), PeopleErrors.InvalidDateOfBirth);
        }

        [Theory]
        [InlineData("1234")]
        [InlineData("123 456 789")]
        [InlineData("1234567890123456789012345678901")]
        [InlineData("12345-678")]
        public void Create_rejects_invalid_national_id(string nationalId)
        {
            ResultAssert.Failed(PersonProfile.Create("A", "B", null, null, nationalId, null, null, Today, Now), PeopleErrors.InvalidNationalId);
        }

        [Fact]
        public void The_national_id_is_stored_in_upper_case_and_blank_means_none()
        {
            var withLetters = ResultAssert.Succeeded(PersonProfile.Create("A", "B", null, null, " ab12345 ", null, null, Today, Now));
            var blank = ResultAssert.Succeeded(PersonProfile.Create("A", "B", null, null, "  ", null, null, Today, Now));

            Assert.Equal("AB12345", withLetters.NationalId);
            Assert.Null(blank.NationalId);
        }

        [Fact]
        public void Create_rejects_invalid_email_phone_and_gender()
        {
            ResultAssert.Failed(PersonProfile.Create("A", "B", null, null, null, "not-an-email", null, Today, Now), PeopleErrors.InvalidEmail);
            ResultAssert.Failed(PersonProfile.Create("A", "B", null, null, null, null, "12", Today, Now), PeopleErrors.InvalidPhone);
            ResultAssert.Failed(PersonProfile.Create("A", "B", null, (Gender)9, null, null, null, Today, Now), PeopleErrors.InvalidGender);
        }

        [Fact]
        public void Update_with_invalid_data_changes_nothing()
        {
            var person = NewPerson();

            ResultAssert.Failed(person.Update("New", "Name", null, null, "bad", null, null, Today, Now), PeopleErrors.InvalidNationalId);

            Assert.Equal("Nour", person.FirstName);
            Assert.Equal("29901011234567", person.NationalId);
            Assert.Null(person.UpdatedAt);
        }

        [Fact]
        public void LinkUser_works_once()
        {
            var person = NewPerson();
            var userId = Guid.NewGuid();

            ResultAssert.Succeeded(person.LinkUser(userId, Now));

            Assert.Equal(userId, person.UserId);
            ResultAssert.Failed(person.LinkUser(Guid.NewGuid(), Now), PeopleErrors.PersonAlreadyLinked);
            Assert.Equal(userId, person.UserId);
        }

        [Fact]
        public void LinkUser_rejects_an_empty_user_id()
        {
            ResultAssert.Failed(NewPerson().LinkUser(Guid.Empty, Now), PeopleErrors.InvalidUser);
        }
    }

    public class EmployeeTests
    {
        private static readonly DateTimeOffset Now = new(2026, 10, 12, 9, 0, 0, TimeSpan.Zero);
        private static readonly Guid PersonId = Guid.NewGuid();
        private static readonly DateOnly HireDate = new(2024, 9, 1);

        private static Employee NewEmployee() => ResultAssert.Succeeded(Employee.Create(PersonId, "emp-001", Guid.NewGuid(), "Lecturer", HireDate, Now));

        [Fact]
        public void Create_normalizes_the_number_and_starts_active()
        {
            var employee = ResultAssert.Succeeded(Employee.Create(PersonId, " emp-001 ", null, "  ", HireDate, Now));

            Assert.Equal("EMP-001", employee.EmployeeNumber);
            Assert.Null(employee.DepartmentId);
            Assert.Null(employee.JobTitle);
            Assert.Equal(EmployeeStatus.Active, employee.Status);
        }

        [Fact]
        public void Create_rejects_empty_person_empty_department_and_bad_number()
        {
            ResultAssert.Failed(Employee.Create(Guid.Empty, "EMP-001", null, null, HireDate, Now), PeopleErrors.InvalidParent);
            ResultAssert.Failed(Employee.Create(PersonId, "EMP-001", Guid.Empty, null, HireDate, Now), PeopleErrors.InvalidParent);
            ResultAssert.Failed(Employee.Create(PersonId, "E", null, null, HireDate, Now), PeopleErrors.InvalidEmployeeNumber);
            ResultAssert.Failed(Employee.Create(PersonId, null, null, null, HireDate, Now), PeopleErrors.InvalidEmployeeNumber);
            ResultAssert.Failed(Employee.Create(PersonId, "EMP 1", null, null, HireDate, Now), PeopleErrors.InvalidEmployeeNumber);
        }

        [Fact]
        public void Create_rejects_a_job_title_longer_than_100_characters()
        {
            ResultAssert.Failed(Employee.Create(PersonId, "EMP-001", null, new string('a', 101), HireDate, Now), PeopleErrors.InvalidJobTitle);
        }

        [Fact]
        public void Update_changes_department_and_title_but_never_the_number()
        {
            var employee = NewEmployee();
            var department = Guid.NewGuid();

            ResultAssert.Succeeded(employee.Update(department, "Professor", Now));

            Assert.Equal(department, employee.DepartmentId);
            Assert.Equal("Professor", employee.JobTitle);
            Assert.Equal("EMP-001", employee.EmployeeNumber);
        }

        [Fact]
        public void Terminate_sets_the_date_and_raises_one_event()
        {
            var employee = NewEmployee();

            ResultAssert.Succeeded(employee.Terminate(new DateOnly(2026, 12, 31), Now));

            Assert.Equal(EmployeeStatus.Terminated, employee.Status);
            Assert.Equal(new DateOnly(2026, 12, 31), employee.TerminationDate);
            var raised = Assert.IsType<EmployeeTerminated>(Assert.Single(employee.DomainEvents));
            Assert.Equal(employee.Id, raised.EmployeeId);
            Assert.Equal(new DateOnly(2026, 12, 31), raised.TerminationDate);
        }

        [Fact]
        public void Terminate_rejects_a_date_before_hiring_and_a_second_termination()
        {
            var employee = NewEmployee();

            ResultAssert.Failed(employee.Terminate(HireDate.AddDays(-1), Now), PeopleErrors.InvalidTerminationDate);
            Assert.Equal(EmployeeStatus.Active, employee.Status);
            Assert.Empty(employee.DomainEvents);

            ResultAssert.Succeeded(employee.Terminate(HireDate, Now));
            ResultAssert.Failed(employee.Terminate(HireDate.AddDays(5), Now), PeopleErrors.EmployeeAlreadyTerminated);
            Assert.Single(employee.DomainEvents);
        }

        [Fact]
        public void A_terminated_employee_cannot_be_updated()
        {
            var employee = NewEmployee();
            ResultAssert.Succeeded(employee.Terminate(HireDate, Now));

            ResultAssert.Failed(employee.Update(null, "x", Now), PeopleErrors.EmployeeNotActive);
        }
    }

    public class InstructorTests
    {
        private static readonly DateTimeOffset Now = new(2026, 10, 12, 9, 0, 0, TimeSpan.Zero);

        [Fact]
        public void Create_with_valid_data_starts_active_and_has_no_department()
        {
            var instructor = ResultAssert.Succeeded(Instructor.Create(Guid.NewGuid(), AcademicRank.AssistantProfessor, " AI ", Now));

            Assert.Equal(AcademicRank.AssistantProfessor, instructor.Rank);
            Assert.Equal("AI", instructor.Specialization);
            Assert.True(instructor.IsActive);
            Assert.Null(typeof(Instructor).GetProperty("DepartmentId"));
        }

        [Fact]
        public void Create_rejects_empty_employee_undefined_rank_and_long_specialization()
        {
            ResultAssert.Failed(Instructor.Create(Guid.Empty, AcademicRank.Lecturer, null, Now), PeopleErrors.InvalidParent);
            ResultAssert.Failed(Instructor.Create(Guid.NewGuid(), (AcademicRank)9, null, Now), PeopleErrors.InvalidAcademicRank);
            ResultAssert.Failed(Instructor.Create(Guid.NewGuid(), AcademicRank.Lecturer, new string('a', 201), Now), PeopleErrors.InvalidSpecialization);
        }

        [Fact]
        public void Update_and_state_changes_follow_the_rules()
        {
            var instructor = ResultAssert.Succeeded(Instructor.Create(Guid.NewGuid(), AcademicRank.Lecturer, null, Now));

            ResultAssert.Succeeded(instructor.Update(AcademicRank.Professor, "Databases", Now));
            Assert.Equal(AcademicRank.Professor, instructor.Rank);

            ResultAssert.Failed(instructor.Update((AcademicRank)9, null, Now), PeopleErrors.InvalidAcademicRank);
            Assert.Equal(AcademicRank.Professor, instructor.Rank);

            ResultAssert.Failed(instructor.Activate(Now), PeopleErrors.AlreadyActive);
            ResultAssert.Succeeded(instructor.Deactivate(Now));
            ResultAssert.Failed(instructor.Deactivate(Now), PeopleErrors.AlreadyInactive);
            ResultAssert.Succeeded(instructor.Activate(Now));
        }
    }

    public class TeachingAssistantTests
    {
        private static readonly DateTimeOffset Now = new(2026, 10, 12, 9, 0, 0, TimeSpan.Zero);

        [Fact]
        public void Create_needs_an_employee_or_a_student()
        {
            ResultAssert.Failed(TeachingAssistant.Create(null, null, Now), PeopleErrors.TaRequiresEmployeeOrStudent);
            ResultAssert.Failed(TeachingAssistant.Create(Guid.Empty, Guid.Empty, Now), PeopleErrors.TaRequiresEmployeeOrStudent);
        }

        [Fact]
        public void Create_accepts_an_employee_only_a_student_only_or_both()
        {
            var employee = Guid.NewGuid();
            var student = Guid.NewGuid();

            var byEmployee = ResultAssert.Succeeded(TeachingAssistant.Create(employee, null, Now));
            var byStudent = ResultAssert.Succeeded(TeachingAssistant.Create(null, student, Now));
            var both = ResultAssert.Succeeded(TeachingAssistant.Create(employee, student, Now));

            Assert.Equal(employee, byEmployee.EmployeeId);
            Assert.Null(byEmployee.StudentId);
            Assert.Null(byStudent.EmployeeId);
            Assert.Equal(student, byStudent.StudentId);
            Assert.Equal(employee, both.EmployeeId);
            Assert.Equal(student, both.StudentId);
            Assert.True(both.IsActive);
        }

        [Fact]
        public void An_empty_guid_counts_as_missing_for_that_side()
        {
            var assistant = ResultAssert.Succeeded(TeachingAssistant.Create(Guid.Empty, Guid.NewGuid(), Now));

            Assert.Null(assistant.EmployeeId);
        }

        [Fact]
        public void Deactivate_and_Activate_follow_the_state_rules()
        {
            var assistant = ResultAssert.Succeeded(TeachingAssistant.Create(Guid.NewGuid(), null, Now));

            ResultAssert.Failed(assistant.Activate(Now), PeopleErrors.AlreadyActive);
            ResultAssert.Succeeded(assistant.Deactivate(Now));
            ResultAssert.Failed(assistant.Deactivate(Now), PeopleErrors.AlreadyInactive);
            ResultAssert.Succeeded(assistant.Activate(Now));
        }
    }
}
