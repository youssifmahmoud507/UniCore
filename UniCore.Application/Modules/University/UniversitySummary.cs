namespace UniCore.Application.Modules.University
{
    public sealed record UniversitySummary(Guid Id, string Code, string Name, string? Description, string? Email, string? Phone, string? Website, bool IsActive, DateTimeOffset CreatedAt, DateTimeOffset? UpdatedAt);
}
