namespace UniCore.Api.Contracts.Users
{
    public sealed record CreateUserRequest(string? UserName, string? Email, string? PhoneNumber);
}
