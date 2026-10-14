using UniCore.Application.Common.Specifications;
using UniCore.Domain.Modules.Finance;

namespace UniCore.Application.Modules.Finance.Specifications
{
    // The key must already be normalized.
    public sealed class PaymentByIdempotencyKeySpecification : Specification<Payment>
    {
        public PaymentByIdempotencyKeySpecification(string key)
        {
            SetCriteria(p => p.IdempotencyKey == key);
        }
    }

}
