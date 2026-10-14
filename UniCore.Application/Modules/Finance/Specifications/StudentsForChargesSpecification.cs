using UniCore.Application.Common.Pagination;
using UniCore.Application.Common.Specifications;
using UniCore.Domain.Modules.Students;

namespace UniCore.Application.Modules.Finance.Specifications
{
    // Active students that currently sit in a program (in the given program when it is set).
    public sealed class StudentsForChargesSpecification : Specification<Student>
    {
        public StudentsForChargesSpecification(PaginationQuery page, Guid? academicProgramId)
        {
            SetCriteria(s => s.AcademicStatus == StudentAcademicStatus.Active && s.ProgramHistory.Any(h => h.EndDate == null && (academicProgramId == null || h.AcademicProgramId == academicProgramId)));

            AddInclude("ProgramHistory");
            ApplyOrderBy(s => s.StudentNumber);
            ApplyPaging(page);
        }
    }

}
