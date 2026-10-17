namespace UniCore.Api.Contracts.Finance
{
    public sealed record RequestRefundRequest(Guid? PaymentId, decimal? Amount, string? Reason);
}
