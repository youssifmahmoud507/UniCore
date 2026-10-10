namespace UniCore.Application.Modules.Identity
{
    public sealed record LoginCommand(string? Login, string? Password, string? IpAddress, string? UserAgent);
}
