using UniCore.Application.Common.Specifications;
using UniCore.Domain.Modules.Finance;

namespace UniCore.Application.Modules.Finance.Specifications
{
    public sealed class PaymentsForInvoiceSpecification : Specification<Payment>
    {
        public PaymentsForInvoiceSpecification(Guid invoiceId)
        {
            SetCriteria(p => p.InvoiceId == invoiceId);
            ApplyOrderBy(p => p.CreatedAt);
        }
    }

}
