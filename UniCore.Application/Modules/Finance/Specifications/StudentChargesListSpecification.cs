using UniCore.Application.Common.Pagination;
using UniCore.Application.Common.Specifications;
using UniCore.Domain.Modules.Finance;

namespace UniCore.Application.Modules.Finance.Specifications
{
    public sealed class StudentChargesListSpecification : Specification<StudentCharge>
    {
        public StudentChargesListSpecification(PaginationQuery query, Guid? studentId, Guid? academicSemesterId, ChargeStatus? status)
        {
            SetCriteria(c => (studentId == null || c.StudentId == studentId) && (academicSemesterId == null || c.AcademicSemesterId == academicSemesterId) && (status == null || c.Status == status));

            ApplyOrderByDescending(c => c.CreatedAt);
            ApplyPaging(query);
        }
    }

}
