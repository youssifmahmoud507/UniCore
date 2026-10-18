using UniCore.Application.Common.Pagination;
using UniCore.Application.Common.Specifications;
using UniCore.Domain.Modules.Finance;

namespace UniCore.Application.Modules.Finance.Specifications
{
    public sealed class HoldsListSpecification : Specification<FinancialHold>
    {
        public HoldsListSpecification(PaginationQuery query, Guid? studentId, FinancialHoldStatus? status, FinancialHoldReason? reason)
        {
            SetCriteria(h => (studentId == null || h.StudentId == studentId)
                && (status == null || h.Status == status)
                && (reason == null || h.Reason == reason));

            ApplyOrderByDescending(h => h.PlacedAt);
            ApplyPaging(query);
        }
    }

}
