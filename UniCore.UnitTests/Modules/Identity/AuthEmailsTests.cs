using UniCore.Application.Modules.Identity;

namespace UniCore.UnitTests.Modules.Identity
{
    public class AuthEmailsTests
    {
        [Fact]
        public void Otp_email_contains_the_code_in_both_bodies_and_the_expiry()
        {
            var email = AuthEmails.PasswordResetOtp("a@uni.edu", "UniCore", "042917", 10);

            Assert.Equal("a@uni.edu", email.To);
            Assert.Contains("042917", email.HtmlBody);
            Assert.Contains("042917", email.TextBody);
            Assert.Contains("10 minutes", email.HtmlBody);
            Assert.DoesNotContain("042917", email.Subject);
        }

        [Fact]
        public void App_name_is_html_encoded_in_the_html_body()
        {
            var email = AuthEmails.PasswordChanged("a@uni.edu", "<b>Evil</b>");

            Assert.DoesNotContain("<b>Evil</b>", email.HtmlBody);
            Assert.Contains("&lt;b&gt;Evil&lt;/b&gt;", email.HtmlBody);
        }
    }



}
