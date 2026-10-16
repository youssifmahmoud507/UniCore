using UniCore.Application.Common.Pagination;
using UniCore.Application.Common.Sorting;
using UniCore.Application.Common.Specifications;
using UniCore.Domain.Modules.People;

namespace UniCore.Application.Modules.People.Specifications
{
    public sealed class PersonsListSpecification : Specification<PersonProfile>
    {
        public static readonly string[] SortFields = ["lastName", "firstName", "createdAt"];

        // The search matches names and email only. The national id is never searchable by text.
        public PersonsListSpecification(PaginationQuery query, string? search, SortOption sort)
        {
            SetCriteria(p => search == null || p.FirstName.StartsWith(search) || p.LastName.StartsWith(search) || (p.Email != null && p.Email.StartsWith(search)));

            switch (sort.Field)
            {
                case "firstName":
                    if (sort.Descending) { ApplyOrderByDescending(p => p.FirstName); } else { ApplyOrderBy(p => p.FirstName); }
                    break;
                case "createdAt":
                    if (sort.Descending) { ApplyOrderByDescending(p => p.CreatedAt); } else { ApplyOrderBy(p => p.CreatedAt); }
                    break;
                default:
                    if (sort.Descending) { ApplyOrderByDescending(p => p.LastName); } else { ApplyOrderBy(p => p.LastName); }
                    break;
            }

            ApplyPaging(query);
        }
    }

    public sealed class PersonByNationalIdSpecification : Specification<PersonProfile>
    {
        public PersonByNationalIdSpecification(string nationalId, Guid? excludeId)
        {
            SetCriteria(p => p.NationalId == nationalId && (excludeId == null || p.Id != excludeId));
        }
    }

    public sealed class PersonByUserSpecification : Specification<PersonProfile>
    {
        public PersonByUserSpecification(Guid userId)
        {
            SetCriteria(p => p.UserId == userId);
        }
    }

    public sealed class EmployeesListSpecification : Specification<Employee>
    {
        public static readonly string[] SortFields = ["employeeNumber", "hireDate"];

        public EmployeesListSpecification(PaginationQuery query, string? search, EmployeeStatus? status, Guid? departmentId, SortOption sort)
        {
            SetCriteria(e => (status == null || e.Status == status)
                && (departmentId == null || e.DepartmentId == departmentId)
                && (search == null || e.EmployeeNumber.StartsWith(search) || (e.JobTitle != null && e.JobTitle.Contains(search))));

            if (sort.Field == "hireDate")
            {
                if (sort.Descending) { ApplyOrderByDescending(e => e.HireDate); } else { ApplyOrderBy(e => e.HireDate); }
            }
            else
            {
                if (sort.Descending) { ApplyOrderByDescending(e => e.EmployeeNumber); } else { ApplyOrderBy(e => e.EmployeeNumber); }
            }

            ApplyPaging(query);
        }
    }

    public sealed class EmployeeByNumberSpecification : Specification<Employee>
    {
        public EmployeeByNumberSpecification(string employeeNumber)
        {
            SetCriteria(e => e.EmployeeNumber == employeeNumber);
        }
    }

    public sealed class EmployeeByPersonSpecification : Specification<Employee>
    {
        public EmployeeByPersonSpecification(Guid personProfileId)
        {
            SetCriteria(e => e.PersonProfileId == personProfileId);
        }
    }

    public sealed class InstructorsListSpecification : Specification<Instructor>
    {
        public static readonly string[] SortFields = ["rank", "createdAt"];

        public InstructorsListSpecification(PaginationQuery query, bool? isActive, AcademicRank? rank, Guid? employeeId, SortOption sort)
        {
            SetCriteria(i => (isActive == null || i.IsActive == isActive)
                && (rank == null || i.Rank == rank)
                && (employeeId == null || i.EmployeeId == employeeId));

            if (sort.Field == "createdAt")
            {
                if (sort.Descending) { ApplyOrderByDescending(i => i.CreatedAt); } else { ApplyOrderBy(i => i.CreatedAt); }
            }
            else
            {
                if (sort.Descending) { ApplyOrderByDescending(i => i.Rank); } else { ApplyOrderBy(i => i.Rank); }
            }

            ApplyPaging(query);
        }
    }

    public sealed class InstructorByEmployeeSpecification : Specification<Instructor>
    {
        public InstructorByEmployeeSpecification(Guid employeeId)
        {
            SetCriteria(i => i.EmployeeId == employeeId);
        }
    }

    public sealed class TeachingAssistantsListSpecification : Specification<TeachingAssistant>
    {
        public static readonly string[] SortFields = ["createdAt"];

        public TeachingAssistantsListSpecification(PaginationQuery query, bool? isActive, SortOption sort)
        {
            SetCriteria(t => isActive == null || t.IsActive == isActive);

            if (sort.Descending) { ApplyOrderByDescending(t => t.CreatedAt); } else { ApplyOrderBy(t => t.CreatedAt); }

            ApplyPaging(query);
        }
    }

    public sealed class TeachingAssistantByEmployeeSpecification : Specification<TeachingAssistant>
    {
        public TeachingAssistantByEmployeeSpecification(Guid employeeId)
        {
            SetCriteria(t => t.EmployeeId == employeeId);
        }
    }

    public sealed class TeachingAssistantByStudentSpecification : Specification<TeachingAssistant>
    {
        public TeachingAssistantByStudentSpecification(Guid studentId)
        {
            SetCriteria(t => t.StudentId == studentId);
        }
    }
}
