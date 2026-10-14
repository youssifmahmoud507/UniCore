using System.Text.RegularExpressions;
using UniCore.Domain.Modules.Identity;

namespace UniCore.IntegrationTests.Infrastructure
{
    // Everyday helpers: log in, create users through the real admin API, set passwords through the real OTP flow.
    public sealed class TestApi
    {
        public const string DefaultPassword = "Str0ng#Passw0rd!";

        private static readonly Regex OtpPattern = new(@"\b\d{6}\b", RegexOptions.Compiled);

        private readonly Lazy<Task<string>> _adminToken;

        public TestApi(ApiClient client, CapturingEmailSender emails)
        {
            Client = client;
            Emails = emails;
            _adminToken = new Lazy<Task<string>>(() => LoginTokenAsync(ApiFixture.AdminLogin, ApiFixture.AdminPassword));
        }

        public ApiClient Client { get; }

        public CapturingEmailSender Emails { get; }

        public static string NewName(string prefix) => $"{prefix}{Guid.NewGuid():N}";

        public Task<string> AdminTokenAsync() => _adminToken.Value;

        public Task<ApiResponse> LoginAsync(string login, string password)
            => Client.PostAsync("/api/v1/auth/login", new { login, password });

        public async Task<string> LoginTokenAsync(string login, string password)
        {
            var response = await LoginAsync(login, password);
            Assert.Equal(200, response.Code);
            return response.Str("accessToken");
        }

        // Creates a user as the admin, optionally gives a role (with a scope), and sets a password
        // through forgot-password -> verify-otp -> reset-password.
        public async Task<TestUser> CreateUserAsync(string prefix, string? role = null, ScopeType? scopeType = null, Guid? scopeId = null, bool setPassword = true)
        {
            var admin = await AdminTokenAsync();
            var userName = NewName(prefix);
            var email = $"{userName}@uni.test";

            var created = await Client.PostAsync("/api/v1/users", new { userName, email }, admin);
            Assert.Equal(201, created.Code);
            var id = created.GuidOf("id");

            if (role is not null)
            {
                var assigned = await Client.PostAsync($"/api/v1/users/{id}/roles", new { role, scopeType, scopeId }, admin);
                Assert.Equal(200, assigned.Code);
            }

            if (setPassword)
            {
                await SetPasswordAsync(email, DefaultPassword);
            }

            return new TestUser(id, userName, email, DefaultPassword);
        }

        public async Task<string> RequestOtpAsync(string email)
        {
            var skip = Emails.Count;

            var forgot = await Client.PostAsync("/api/v1/auth/forgot-password", new { email });
            Assert.Equal(200, forgot.Code);

            var mail = await Emails.WaitForAsync(m => m.To == email && m.Subject.Contains("reset code", StringComparison.OrdinalIgnoreCase),skip,TimeSpan.FromSeconds(10));

            Assert.NotNull(mail);

            var match = OtpPattern.Match(mail.TextBody);
            Assert.True(match.Success);

            return match.Value;
        }

        public async Task<string> VerifyOtpAsync(string email, string otp)
        {
            var verified = await Client.PostAsync("/api/v1/auth/verify-otp", new { email, otp });
            Assert.Equal(200, verified.Code);
            return verified.Str("resetToken");
        }

        public async Task SetPasswordAsync(string email, string password)
        {
            var otp = await RequestOtpAsync(email);
            var resetToken = await VerifyOtpAsync(email, otp);

            var reset = await Client.PostAsync(
                "/api/v1/auth/reset-password",
                new { email, resetToken, newPassword = password, confirmPassword = password });

            Assert.Equal(200, reset.Code);
        }
    }


    public record TestUser(Guid Id,string UserName,string Email,string Password);


}
