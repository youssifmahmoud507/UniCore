using UniCore.Application.Common.Specifications;
using UniCore.Domain.Modules.Finance;

namespace UniCore.Application.Modules.Finance.Specifications
{
    // Unpaid invoices whose due date is before the cutoff (see FinancialHoldRules.OverdueCutoff).
    public sealed class OverdueInvoicesForStudentSpecification : Specification<Invoice>
    {
        public OverdueInvoicesForStudentSpecification(Guid studentId, DateOnly cutoff)
        {
            SetCriteria(i => i.StudentId == studentId
                && (i.Status == InvoiceStatus.Issued || i.Status == InvoiceStatus.PartiallyPaid)
                && i.DueDate < cutoff);
        }
    }

}
