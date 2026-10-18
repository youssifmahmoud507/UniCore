using UniCore.Application.Common.Pagination;
using UniCore.Application.Common.Specifications;
using UniCore.Domain.Modules.Finance;

namespace UniCore.Application.Modules.Finance.Specifications
{
    public sealed class RefundsListSpecification : Specification<Refund>
    {
        public RefundsListSpecification(PaginationQuery query, RefundStatus? status, Guid? studentId, Guid? invoiceId)
        {
            SetCriteria(r => (status == null || r.Status == status)
                && (studentId == null || r.StudentId == studentId)
                && (invoiceId == null || r.InvoiceId == invoiceId));

            ApplyOrderByDescending(r => r.RequestedAt);
            ApplyPaging(query);
        }
    }






}
