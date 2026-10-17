using UniCore.Application.Common.Specifications;
using UniCore.Domain.Modules.Finance;

namespace UniCore.Application.Modules.Finance.Specifications
{
    public sealed class InvoicesForBalanceSpecification : Specification<Invoice>
    {
        public InvoicesForBalanceSpecification(Guid studentId)
        {
            SetCriteria(i => i.StudentId == studentId && i.Status != InvoiceStatus.Cancelled);
        }
    }

}
