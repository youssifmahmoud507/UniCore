using UniCore.Application.Modules.Identity;
using UniCore.UnitTests.Common;

namespace UniCore.UnitTests.Modules.Identity
{
    internal sealed class FakeTokenService : ITokenService
    {
        private readonly FakeClock _clock;
        private int _counter;

        public FakeTokenService(FakeClock clock)
        {
            _clock = clock;
        }

        public AccessTokenResult CreateAccessToken(TokenSubject subject)
            => new($"access-{subject.UserId}", _clock.UtcNow.AddMinutes(15));

        public GeneratedRefreshToken GenerateRefreshToken()
        {
            _counter++;
            var raw = $"raw-{_counter}";
            return new GeneratedRefreshToken(raw, HashRefreshToken(raw), _clock.UtcNow.AddDays(7));
        }

        public string HashRefreshToken(string rawToken) => $"hash:{rawToken}";
    }
}
