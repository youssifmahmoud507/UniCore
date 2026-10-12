using UniCore.Application.Common.Pagination;
using UniCore.Application.Common.Specifications;
using UniCore.Domain.Modules.Students;

namespace UniCore.Application.Modules.Students.Specifications
{
    public sealed class StudentsListSpecification : Specification<Student>
    {
        // numberPrefix must already be upper case: student numbers are stored upper case.
        public StudentsListSpecification(PaginationQuery query, StudentAcademicStatus? status, string? numberPrefix)
        {
            if (status is { } wanted && !string.IsNullOrEmpty(numberPrefix))
            {
                SetCriteria(s => s.AcademicStatus == wanted && s.StudentNumber.StartsWith(numberPrefix));
            }
            else if (status is { } onlyStatus)
            {
                SetCriteria(s => s.AcademicStatus == onlyStatus);
            }
            else if (!string.IsNullOrEmpty(numberPrefix))
            {
                SetCriteria(s => s.StudentNumber.StartsWith(numberPrefix));
            }

            AddInclude("Profile");
            AddInclude("ProgramHistory");
            ApplyOrderBy(s => s.StudentNumber);
            ApplyPaging(query);
        }
    }


}
