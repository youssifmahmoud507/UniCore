using UniCore.Application.Common.Pagination;
using UniCore.Application.Common.Specifications;
using UniCore.Domain.Modules.Finance;

namespace UniCore.Application.Modules.Finance.Specifications
{
    public sealed class OverdueInvoicesSpecification : Specification<Invoice>
    {
        public OverdueInvoicesSpecification(DateOnly cutoff, PaginationQuery page)
        {
            SetCriteria(i => (i.Status == InvoiceStatus.Issued || i.Status == InvoiceStatus.PartiallyPaid)
                && i.DueDate < cutoff);

            ApplyOrderBy(i => i.Id);
            ApplyPaging(page);
        }
    }

}
