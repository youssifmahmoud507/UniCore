using UniCore.Domain.Modules.Finance;

namespace UniCore.Application.Modules.Reporting
{
    public sealed record PaymentFact(DateOnly PaymentDate, PaymentMethod Method, decimal Amount, decimal Refunded);




}
