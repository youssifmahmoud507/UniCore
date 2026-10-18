using UniCore.Application.Common.Pagination;
using UniCore.Application.Common.Specifications;
using UniCore.Domain.Modules.Finance;

namespace UniCore.Application.Modules.Finance.Specifications
{
    public sealed class StudentScholarshipsListSpecification : Specification<StudentScholarship>
    {
        public StudentScholarshipsListSpecification(PaginationQuery query, Guid? studentId, StudentScholarshipStatus? status)
        {
            SetCriteria(a => (studentId == null || a.StudentId == studentId) && (status == null || a.Status == status));

            ApplyOrderByDescending(a => a.CreatedAt);
            ApplyPaging(query);
        }
    }






}
