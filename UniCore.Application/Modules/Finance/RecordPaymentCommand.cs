using UniCore.Domain.Modules.Finance;

namespace UniCore.Application.Modules.Finance
{
    // PaymentDate = null means today.
    public sealed record RecordPaymentCommand(decimal? Amount, PaymentMethod? Method, string? Reference, DateOnly? PaymentDate);



}
