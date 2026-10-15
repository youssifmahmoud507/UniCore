namespace UniCore.Api.Contracts.University
{
    public sealed record CreateUniversityRequest(string? Code, string? Name, string? Description, string? Email, string? Phone, string? Website);
}
