using UniCore.Application.Common.Pagination;
using UniCore.Application.Common.Specifications;
using UniCore.Domain.Modules.Finance;

namespace UniCore.Application.Modules.Finance.Specifications
{
    public sealed class PaymentsForInvoicesSpecification : Specification<Payment>
    {
        public PaymentsForInvoicesSpecification(Guid[] invoiceIds, PaginationQuery query)
        {
            SetCriteria(p => invoiceIds.Contains(p.InvoiceId));

            ApplyOrderByDescending(p => p.CreatedAt);
            ApplyPaging(query);
        }
    }


}
