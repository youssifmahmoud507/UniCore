namespace UniCore.Application.Modules.Identity
{
    public interface IOtpService
    {
        // 6 digits from a cryptographically secure generator.
        string Generate();

        // HMAC-SHA256 keyed with a server secret.
        string Hash(Guid userId, string otp);

        // Constant-time comparison.
        bool Verify(string storedHash, Guid userId, string otp);
    }


}
