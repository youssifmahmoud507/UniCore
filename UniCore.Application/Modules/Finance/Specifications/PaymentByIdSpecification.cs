using UniCore.Application.Common.Specifications;
using UniCore.Domain.Modules.Finance;

namespace UniCore.Application.Modules.Finance.Specifications
{
    public sealed class PaymentByIdSpecification : Specification<Payment>
    {
        public PaymentByIdSpecification(Guid id)
        {
            SetCriteria(p => p.Id == id);
        }
    }






}
