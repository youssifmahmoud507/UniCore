namespace UniCore.Application.Modules.Identity
{
    public sealed record RefreshCommand(string? RefreshToken, string? IpAddress, string? UserAgent);
}
