namespace UniCore.Application.Modules.Identity
{
    public sealed record VerifyOtpResponse(string ResetToken, DateTimeOffset ExpiresAt);


}
