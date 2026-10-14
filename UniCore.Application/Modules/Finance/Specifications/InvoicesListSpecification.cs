using UniCore.Application.Common.Pagination;
using UniCore.Application.Common.Specifications;
using UniCore.Domain.Modules.Finance;

namespace UniCore.Application.Modules.Finance.Specifications
{
    // No lines here: the list shows summaries only.
    public sealed class InvoicesListSpecification : Specification<Invoice>
    {
        public InvoicesListSpecification(PaginationQuery query, Guid? studentId, InvoiceStatus? status)
        {
            SetCriteria(i => (studentId == null || i.StudentId == studentId) && (status == null || i.Status == status));

            ApplyOrderByDescending(i => i.CreatedAt);
            ApplyPaging(query);
        }
    }

}
