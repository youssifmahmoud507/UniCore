using UniCore.Application.Common.Specifications;
using UniCore.Domain.Modules.Finance;

namespace UniCore.Application.Modules.Finance.Specifications
{
    // Tracked: cancelling an invoice changes these charges.
    public sealed class ChargesForInvoiceSpecification : Specification<StudentCharge>
    {
        public ChargesForInvoiceSpecification(Guid invoiceId)
        {
            SetCriteria(c => c.InvoiceId == invoiceId && c.Status == ChargeStatus.Invoiced);
            UseTracking();
        }
    }

}
