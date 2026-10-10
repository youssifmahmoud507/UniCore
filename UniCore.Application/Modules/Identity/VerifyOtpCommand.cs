namespace UniCore.Application.Modules.Identity
{
    public sealed record VerifyOtpCommand(string? Email, string? Otp, string? IpAddress, string? UserAgent);


}
