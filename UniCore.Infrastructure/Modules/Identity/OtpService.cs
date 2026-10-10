using Microsoft.Extensions.Options;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using UniCore.Application.Modules.Identity;

namespace UniCore.Infrastructure.Modules.Identity
{
    public sealed class OtpService : IOtpService
    {
        private readonly byte[] _pepper;

        public OtpService(IOptions<PasswordResetOptions> options)
        {
            _pepper = Encoding.UTF8.GetBytes(options.Value.Pepper);
        }

        // RandomNumberGenerator, never System.Random. "D6" keeps leading zeros.
        public string Generate()
            => RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6", CultureInfo.InvariantCulture);

        public string Hash(Guid userId, string otp)
        {
            using var hmac = new HMACSHA256(_pepper);
            var bytes = hmac.ComputeHash(Encoding.UTF8.GetBytes($"{userId:N}:{otp}"));
            return Convert.ToHexString(bytes);
        }

        public bool Verify(string storedHash, Guid userId, string otp)
        {
            var computed = Encoding.UTF8.GetBytes(Hash(userId, otp));
            var stored = Encoding.UTF8.GetBytes(storedHash);

            return CryptographicOperations.FixedTimeEquals(computed, stored);
        }
    }




}
