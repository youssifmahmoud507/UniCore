namespace UniCore.Api.Contracts.University
{
    public sealed record UpdateUniversityRequest(string? Name, string? Description, string? Email, string? Phone, string? Website);
}
