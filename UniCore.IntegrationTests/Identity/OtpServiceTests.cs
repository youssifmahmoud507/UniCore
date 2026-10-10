using System.Text.RegularExpressions;
using UniCore.Infrastructure.Modules.Identity;

namespace UniCore.IntegrationTests.Identity
{
    public class OtpServiceTests
    {
        private static OtpService Create(string pepper = "unit-test-pepper-unit-test-pepper-123456")
            => new(Microsoft.Extensions.Options.Options.Create(new PasswordResetOptions { Pepper = pepper }));

        [Fact]
        public void Generated_codes_are_always_six_digits()
        {
            var service = Create();

            for (var i = 0; i < 500; i++)
            {
                Assert.Matches(new Regex("^[0-9]{6}$"), service.Generate());
            }
        }

        [Fact]
        public void Hash_is_deterministic_and_never_contains_the_code()
        {
            var service = Create();
            var user = Guid.NewGuid();

            var a = service.Hash(user, "123456");
            var b = service.Hash(user, "123456");

            Assert.Equal(a, b);
            Assert.DoesNotContain("123456", a);
            Assert.Equal(64, a.Length);
        }

        [Fact]
        public void Hash_depends_on_the_user_the_code_and_the_pepper()
        {
            var user = Guid.NewGuid();
            var service = Create();

            Assert.NotEqual(service.Hash(user, "123456"), service.Hash(Guid.NewGuid(), "123456"));
            Assert.NotEqual(service.Hash(user, "123456"), service.Hash(user, "123457"));
            Assert.NotEqual(service.Hash(user, "123456"), Create("another-pepper-another-pepper-1234567").Hash(user, "123456"));
        }

        [Fact]
        public void Verify_accepts_the_right_code_and_rejects_everything_else()
        {
            var service = Create();
            var user = Guid.NewGuid();
            var stored = service.Hash(user, "123456");

            Assert.True(service.Verify(stored, user, "123456"));
            Assert.False(service.Verify(stored, user, "654321"));
            Assert.False(service.Verify(stored, Guid.NewGuid(), "123456"));
            Assert.False(service.Verify("short", user, "123456"));
        }
    }
    }
