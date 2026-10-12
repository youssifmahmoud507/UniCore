using UniCore.Domain.Modules.Students;

namespace UniCore.Application.Modules.Students
{
    public sealed record StatusHistoryItem(Guid Id, StudentAcademicStatus OldStatus, StudentAcademicStatus NewStatus, DateOnly EffectiveDate, string? Reason, Guid ChangedByUserId, DateTimeOffset CreatedAt);
}
