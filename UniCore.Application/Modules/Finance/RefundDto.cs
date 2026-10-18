using UniCore.Domain.Modules.Finance;

namespace UniCore.Application.Modules.Finance
{
    public sealed record RefundDto(Guid Id, Guid PaymentId, Guid InvoiceId, Guid StudentId, decimal Amount, string Reason, RefundStatus Status, Guid? RequestedByUserId, DateTimeOffset RequestedAt, Guid? ReviewedByUserId, DateTimeOffset? ReviewedAt, string? RejectionReason, Guid? ProcessedByUserId, DateTimeOffset? ProcessedAt);






}
