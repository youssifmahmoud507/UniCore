using UniCore.Infrastructure.Modules.Email;
using UniCore.Infrastructure.Modules.Identity;

namespace UniCore.IntegrationTests.Identity
{
    public class EmailOptionsValidatorTests
    {
        private static SmtpOptions ValidSmtp() => new()
        {
            Host = "localhost",
            Port = 1025,
            FromName = "UniCore",
            FromAddress = "no-reply@unicore.local",
            TimeoutSeconds = 15
        };

        [Fact]
        public void Valid_smtp_options_pass()
            => Assert.True(new SmtpOptionsValidator().Validate(null, ValidSmtp()).Succeeded);

        [Fact]
        public void Smtp_options_reject_bad_host_port_address_and_half_credentials()
        {
            var validator = new SmtpOptionsValidator();

            var noHost = ValidSmtp(); noHost.Host = "";
            var badPort = ValidSmtp(); badPort.Port = 0;
            var badFrom = ValidSmtp(); badFrom.FromAddress = "nope";
            var userWithoutPassword = ValidSmtp(); userWithoutPassword.Username = "user";

            Assert.True(validator.Validate(null, noHost).Failed);
            Assert.True(validator.Validate(null, badPort).Failed);
            Assert.True(validator.Validate(null, badFrom).Failed);
            Assert.True(validator.Validate(null, userWithoutPassword).Failed);
        }

        [Fact]
        public void Short_pepper_fails_without_leaking_it()
        {
            var options = new PasswordResetOptions { Pepper = "too-short-pepper" };

            var result = new PasswordResetOptionsValidator().Validate(null, options);

            Assert.True(result.Failed);
            Assert.DoesNotContain("too-short-pepper", string.Join(" ", result.Failures ?? Array.Empty<string>()));
        }

        [Fact]
        public void Valid_password_reset_options_pass()
        {
            var options = new PasswordResetOptions { Pepper = new string('p', 32) };

            Assert.True(new PasswordResetOptionsValidator().Validate(null, options).Succeeded);
        }
    }
    }
