using UniCore.Application.Common.Specifications;
using UniCore.Domain.Modules.Finance;

namespace UniCore.Application.Modules.Finance.Specifications
{
    public sealed class ActiveHoldsForStudentSpecification : Specification<FinancialHold>
    {
        public ActiveHoldsForStudentSpecification(Guid studentId)
        {
            SetCriteria(h => h.StudentId == studentId && h.Status == FinancialHoldStatus.Active);
            ApplyOrderBy(h => h.PlacedAt);
        }
    }




}
