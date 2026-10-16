using UniCore.Application.Common.Repositories;
using UniCore.Application.Modules.People.Specifications;
using UniCore.Application.Modules.Students.Specifications;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.People;
using UniCore.Domain.Modules.Students;
using UniCore.Domain.Modules.University;

namespace UniCore.Application.Modules.People
{
    // Everything the authorization layer needs to know about who a user is in the academic structure.
    // Developer 2's code turns this into scope assignments:
    //   student     -> StudentId (a student sees their own records)
    //   instructor  -> InstructorId and SectionIds (the sections they are assigned to)
    //   dean / head -> FacultyId / DepartmentId (the employee's own department and its faculty)
    //   registrar   -> no data needed: Global is a role assignment, not derived from people data
    public sealed record PersonScopeData(
        Guid PersonProfileId,
        Guid? StudentId,
        Guid? EmployeeId,
        Guid? InstructorId,
        Guid? TeachingAssistantId,
        Guid? DepartmentId,
        Guid? FacultyId,
        IReadOnlyList<Guid> SectionIds);

    // Sections arrive in Week 8 (CourseSection + InstructorAssignment). Until then the infrastructure
    // registers an implementation that returns an empty list, so the scope data is complete in shape.
    public interface ISectionAssignmentReader
    {
        Task<IReadOnlyList<Guid>> GetAssignedSectionIdsAsync(Guid? instructorId, Guid? teachingAssistantId, CancellationToken cancellationToken = default);
    }

    public interface IScopeDataProvider
    {
        // The scope data of the person linked to this login account. PERSON_NOT_FOUND when the account has no person.
        Task<Result<PersonScopeData>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    }

    public sealed class ScopeDataProvider(
        IRepository<PersonProfile> persons, IRepository<Employee> employees, IRepository<Instructor> instructors,
        IRepository<TeachingAssistant> assistants, IRepository<Student> students, IRepository<Department> departments,
        IRepository<Faculty> faculties, ISectionAssignmentReader sections) : IScopeDataProvider
    {
        private readonly IRepository<PersonProfile> _persons = persons;
        private readonly IRepository<Employee> _employees = employees;
        private readonly IRepository<Instructor> _instructors = instructors;
        private readonly IRepository<TeachingAssistant> _assistants = assistants;
        private readonly IRepository<Student> _students = students;
        private readonly IRepository<Department> _departments = departments;
        private readonly IRepository<Faculty> _faculties = faculties;
        private readonly ISectionAssignmentReader _sections = sections;

        public async Task<Result<PersonScopeData>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            var person = await _persons.FirstOrDefaultAsync(new PersonByUserSpecification(userId), cancellationToken);
            if (person is null)
            {
                return PeopleErrors.PersonNotFound;
            }

            var student = await _students.FirstOrDefaultAsync(new StudentByPersonSpecification(person.Id), cancellationToken);
            var employee = await _employees.FirstOrDefaultAsync(new EmployeeByPersonSpecification(person.Id), cancellationToken);

            Instructor? instructor = null;
            TeachingAssistant? assistant = null;
            Guid? departmentId = null;
            Guid? facultyId = null;

            if (employee is not null && employee.Status == EmployeeStatus.Active)
            {
                instructor = await _instructors.FirstOrDefaultAsync(new InstructorByEmployeeSpecification(employee.Id), cancellationToken);
                assistant = await _assistants.FirstOrDefaultAsync(new TeachingAssistantByEmployeeSpecification(employee.Id), cancellationToken);

                if (employee.DepartmentId is { } id)
                {
                    var department = await _departments.GetByIdAsync(id, cancellationToken);
                    departmentId = department?.Id;
                    facultyId = department is null ? null : (await _faculties.GetByIdAsync(department.FacultyId, cancellationToken))?.Id;
                }
            }

            if (assistant is null && student is not null)
            {
                assistant = await _assistants.FirstOrDefaultAsync(new TeachingAssistantByStudentSpecification(student.Id), cancellationToken);
            }

            // Only active instructors and assistants are assigned to sections.
            var activeInstructorId = instructor is { IsActive: true } ? instructor.Id : (Guid?)null;
            var activeAssistantId = assistant is { IsActive: true } ? assistant.Id : (Guid?)null;
            var sectionIds = await _sections.GetAssignedSectionIdsAsync(activeInstructorId, activeAssistantId, cancellationToken);

            return new PersonScopeData(person.Id, student?.Id, employee?.Id, instructor?.Id, assistant?.Id, departmentId, facultyId, sectionIds);
        }
    }
}
