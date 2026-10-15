namespace UniCore.Api.Contracts.University
{
    public sealed record UpdateDepartmentRequest(string? Name, string? Description, string? Email, string? Phone);
}
