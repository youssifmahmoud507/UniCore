namespace UniCore.Application.Modules.Identity
{
    public sealed record LogoutCommand(string? RefreshToken, string? IpAddress, string? UserAgent);
}
