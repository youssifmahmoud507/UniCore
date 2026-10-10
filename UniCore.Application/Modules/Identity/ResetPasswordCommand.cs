namespace UniCore.Application.Modules.Identity
{
    public sealed record ResetPasswordCommand(string? Email, string? ResetToken, string? NewPassword, string? ConfirmPassword, string? IpAddress, string? UserAgent);


}
