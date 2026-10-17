using UniCore.Application.Common.Pagination;
using UniCore.Application.Common.Specifications;
using UniCore.Domain.Modules.Finance;

namespace UniCore.Application.Modules.Finance.Specifications
{
    // Read-only and ordered by Id, so it can be paged safely (nothing changes while it is read).
    public sealed class ActiveAutomaticHoldsSpecification : Specification<FinancialHold>
    {
        public ActiveAutomaticHoldsSpecification(PaginationQuery page)
        {
            SetCriteria(h => h.Status == FinancialHoldStatus.Active && h.Reason == FinancialHoldReason.OverdueBalance);
            ApplyOrderBy(h => h.Id);
            ApplyPaging(page);
        }
    }

}
