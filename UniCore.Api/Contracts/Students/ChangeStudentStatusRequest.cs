using UniCore.Domain.Modules.Students;

namespace UniCore.Api.Contracts.Students
{
    public sealed record ChangeStudentStatusRequest(StudentAcademicStatus? NewStatus, DateOnly? EffectiveDate, string? Reason);
}
