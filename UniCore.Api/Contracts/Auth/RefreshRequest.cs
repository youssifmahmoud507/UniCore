namespace UniCore.Api.Contracts.Auth
{
    public sealed record RefreshRequest(string? RefreshToken);

    public sealed record ForgotPasswordRequest(string? Email);

    public sealed record VerifyOtpRequest(string? Email, string? Otp);

    public sealed record ResetPasswordRequest(string? Email, string? ResetToken, string? NewPassword, string? ConfirmPassword);

    public sealed record MessageResponse(string Message);


}
