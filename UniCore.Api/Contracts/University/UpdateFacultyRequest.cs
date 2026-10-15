namespace UniCore.Api.Contracts.University
{
    public sealed record UpdateFacultyRequest(string? Name, string? Description, string? Email, string? Phone);
}
