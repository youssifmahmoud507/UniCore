using UniCore.Application.Modules.Identity;

namespace UniCore.UnitTests.Modules.Identity
{
    internal sealed class FakeOtpService : IOtpService
    {
        public string Next { get; set; } = "123456";

        public string Generate() => Next;
        public string Hash(Guid userId, string otp) => $"h:{userId:N}:{otp}";
        public bool Verify(string storedHash, Guid userId, string otp) => storedHash == Hash(userId, otp);
    }



}
