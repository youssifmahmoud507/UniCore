namespace UniCore.Application.Modules.University
{
    public sealed record FacultySummary(Guid Id, Guid UniversityId, string Code, string Name, string? Description, string? Email, string? Phone, bool IsActive, DateTimeOffset CreatedAt, DateTimeOffset? UpdatedAt);
}
