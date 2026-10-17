namespace UniCore.Application.Modules.Finance
{
    public sealed record RequestRefundCommand(Guid? PaymentId, decimal? Amount, string? Reason);






}
