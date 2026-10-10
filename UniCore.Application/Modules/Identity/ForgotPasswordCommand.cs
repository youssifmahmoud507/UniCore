namespace UniCore.Application.Modules.Identity
{
    public sealed record ForgotPasswordCommand(string? Email, string? IpAddress, string? UserAgent);


}
