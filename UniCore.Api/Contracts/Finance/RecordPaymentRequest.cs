using UniCore.Domain.Modules.Finance;

namespace UniCore.Api.Contracts.Finance
{
    // PaymentDate = null means today.
    public sealed record RecordPaymentRequest(decimal? Amount, PaymentMethod? Method, string? Reference, DateOnly? PaymentDate);
}
