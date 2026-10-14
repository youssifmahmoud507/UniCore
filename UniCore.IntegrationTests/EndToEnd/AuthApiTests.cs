using System;
using System.Collections.Generic;
using UniCore.Domain.Modules.Identity;
using UniCore.IntegrationTests.Infrastructure;

namespace UniCore.IntegrationTests.EndToEnd
{
    [Collection(ApiCollection.Name)]
    public sealed class AuthApiTests(ApiFixture fixture)
    {
        private readonly TestApi _api = fixture.Api;

        private ApiClient Client => _api.Client;

        [Fact]
        public async Task Admin_logs_in_and_sees_the_full_access()
        {
            var login = await _api.LoginAsync(ApiFixture.AdminLogin, ApiFixture.AdminPassword);

            Assert.Equal(200, login.Code);
            Assert.Equal("Bearer", login.Str("tokenType"));
            Assert.False(string.IsNullOrEmpty(login.Str("refreshToken")));

            var me = await Client.GetAsync("/api/v1/me", login.Str("accessToken"));

            Assert.Equal(200, me.Code);
            Assert.Contains(RoleNames.SuperAdmin, me.Prop("roles").EnumerateArray().Select(e => e.GetString()));
            Assert.Equal(Permissions.AllCodes.Count, me.Prop("permissions").GetArrayLength());

            var scope = Assert.Single(me.Prop("scopes").EnumerateArray());
            Assert.Equal("Global", scope.GetProperty("scopeType").GetString());
        }

        [Fact]
        public async Task Missing_credentials_are_a_validation_error()
        {
            var response = await Client.PostAsync("/api/v1/auth/login", new { login = "", password = "" });

            Assert.Equal(400, response.Code);
            Assert.Equal("CREDENTIALS_REQUIRED", response.ErrorCode);
        }

        [Fact]
        public async Task A_wrong_password_and_an_unknown_user_get_the_same_answer()
        {
            var user = await _api.CreateUserAsync("wrongpw");

            var wrongPassword = await _api.LoginAsync(user.UserName, "Wrong#Passw0rd1");
            var unknownUser = await _api.LoginAsync(TestApi.NewName("nobody"), "Wrong#Passw0rd1");

            Assert.Equal(401, wrongPassword.Code);
            Assert.Equal(401, unknownUser.Code);
            Assert.Equal("INVALID_CREDENTIALS", wrongPassword.ErrorCode);
            Assert.Equal("INVALID_CREDENTIALS", unknownUser.ErrorCode);
            Assert.Equal(wrongPassword.Str("detail"), unknownUser.Str("detail"));
        }

        [Fact]
        public async Task Protected_endpoints_without_a_valid_token_return_problem_details()
        {
            var anonymous = await Client.GetAsync("/api/v1/me");
            var garbage = await Client.GetAsync("/api/v1/me", "not-a-real-token");

            foreach (var response in new[] { anonymous, garbage })
            {
                Assert.Equal(401, response.Code);
                Assert.Equal("GENERAL_UNAUTHORIZED", response.ErrorCode);
                Assert.False(string.IsNullOrEmpty(response.Str("traceId")));
            }
        }

        [Fact]
        public async Task Refresh_rotates_the_token_and_a_reused_token_kills_every_session()
        {
            var user = await _api.CreateUserAsync("refresh");

            var first = await _api.LoginAsync(user.UserName, user.Password);
            Assert.Equal(200, first.Code);

            var second = await Client.PostAsync("/api/v1/auth/refresh", new { refreshToken = first.Str("refreshToken") });
            Assert.Equal(200, second.Code);
            Assert.NotEqual(first.Str("refreshToken"), second.Str("refreshToken"));

            var me = await Client.GetAsync("/api/v1/me", second.Str("accessToken"));
            Assert.Equal(200, me.Code);

            // Someone presents the first token again: theft is assumed.
            var reuse = await Client.PostAsync("/api/v1/auth/refresh", new { refreshToken = first.Str("refreshToken") });
            Assert.Equal(401, reuse.Code);
            Assert.Equal("REFRESH_TOKEN_REUSED", reuse.ErrorCode);

            // Even the newest token is dead now.
            var afterwards = await Client.PostAsync("/api/v1/auth/refresh", new { refreshToken = second.Str("refreshToken") });
            Assert.Equal(401, afterwards.Code);
            Assert.Equal("REFRESH_TOKEN_REUSED", afterwards.ErrorCode);
        }

        [Fact]
        public async Task Refresh_with_a_missing_or_unknown_token_is_invalid()
        {
            var missing = await Client.PostAsync("/api/v1/auth/refresh", new { refreshToken = (string?)null });
            var unknown = await Client.PostAsync("/api/v1/auth/refresh", new { refreshToken = "never-issued" });

            Assert.Equal(401, missing.Code);
            Assert.Equal("REFRESH_TOKEN_INVALID", missing.ErrorCode);
            Assert.Equal(401, unknown.Code);
            Assert.Equal("REFRESH_TOKEN_INVALID", unknown.ErrorCode);
        }

        [Fact]
        public async Task Logout_revokes_only_the_callers_own_session()
        {
            var userA = await _api.CreateUserAsync("logouta");
            var userB = await _api.CreateUserAsync("logoutb");
            var a = await _api.LoginAsync(userA.UserName, userA.Password);
            var b = await _api.LoginAsync(userB.UserName, userB.Password);

            // A cannot kill B's session, even with B's refresh token.
            var attempt = await Client.PostAsync(
                "/api/v1/auth/logout", new { refreshToken = b.Str("refreshToken") }, a.Str("accessToken"));
            Assert.Equal(204, attempt.Code);

            var bStillWorks = await Client.PostAsync("/api/v1/auth/refresh", new { refreshToken = b.Str("refreshToken") });
            Assert.Equal(200, bStillWorks.Code);

            // A logs out properly.
            var logout = await Client.PostAsync(
                "/api/v1/auth/logout", new { refreshToken = a.Str("refreshToken") }, a.Str("accessToken"));
            Assert.Equal(204, logout.Code);

            var afterLogout = await Client.PostAsync("/api/v1/auth/refresh", new { refreshToken = a.Str("refreshToken") });
            Assert.Equal(401, afterLogout.Code);
        }

        [Fact]
        public async Task Logout_needs_authentication()
        {
            var response = await Client.PostAsync("/api/v1/auth/logout", new { refreshToken = "x" });

            Assert.Equal(401, response.Code);
            Assert.Equal("GENERAL_UNAUTHORIZED", response.ErrorCode);
        }

        [Fact]
        public async Task Five_wrong_passwords_lock_the_account_even_for_the_right_password()
        {
            var user = await _api.CreateUserAsync("lockout");

            for (var attempt = 1; attempt <= 4; attempt++)
            {
                var wrong = await _api.LoginAsync(user.UserName, "Wrong#Passw0rd1");
                Assert.Equal(401, wrong.Code);
                Assert.Equal("INVALID_CREDENTIALS", wrong.ErrorCode);
            }

            var fifth = await _api.LoginAsync(user.UserName, "Wrong#Passw0rd1");
            Assert.Equal(403, fifth.Code);
            Assert.Equal("ACCOUNT_LOCKED_OUT", fifth.ErrorCode);

            var right = await _api.LoginAsync(user.UserName, user.Password);
            Assert.Equal(403, right.Code);
            Assert.Equal("ACCOUNT_LOCKED_OUT", right.ErrorCode);
        }

        [Fact]
        public async Task Forgot_password_answers_the_same_for_unknown_emails_and_sends_nothing()
        {
            var known = await _api.CreateUserAsync("known", setPassword: false);
            var unknownEmail = $"{TestApi.NewName("ghost")}@uni.test";

            var skip = _api.Emails.Count;
            var unknown = await Client.PostAsync("/api/v1/auth/forgot-password", new { email = unknownEmail });
            var nothing = await _api.Emails.WaitForAsync(m => m.To == unknownEmail, skip, TimeSpan.FromMilliseconds(800));

            var real = await Client.PostAsync("/api/v1/auth/forgot-password", new { email = known.Email });

            Assert.Equal(200, unknown.Code);
            Assert.Equal(200, real.Code);
            Assert.Equal(real.Str("message"), unknown.Str("message"));
            Assert.Null(nothing);
        }

        [Fact]
        public async Task Forgot_password_with_a_malformed_email_is_a_validation_error()
        {
            var response = await Client.PostAsync("/api/v1/auth/forgot-password", new { email = "not-an-email" });

            Assert.Equal(400, response.Code);
            Assert.Equal("USER_EMAIL_INVALID", response.ErrorCode);
        }

        [Fact]
        public async Task Full_password_reset_flow()
        {
            var user = await _api.CreateUserAsync("reset", setPassword: false);
            var otp = await _api.RequestOtpAsync(user.Email);
            var wrong = otp == "000000" ? "111111" : "000000";

            var badVerify = await Client.PostAsync("/api/v1/auth/verify-otp", new { email = user.Email, otp = wrong });
            Assert.Equal(400, badVerify.Code);
            Assert.Equal("OTP_INVALID", badVerify.ErrorCode);

            var resetToken = await _api.VerifyOtpAsync(user.Email, otp);

            // The code is single use.
            var again = await Client.PostAsync("/api/v1/auth/verify-otp", new { email = user.Email, otp });
            Assert.Equal("OTP_INVALID", again.ErrorCode);

            var weak = await Client.PostAsync(
                "/api/v1/auth/reset-password",
                new { email = user.Email, resetToken, newPassword = "short", confirmPassword = "short" });
            Assert.Equal(400, weak.Code);
            Assert.Equal("PASSWORD_POLICY_VIOLATION", weak.ErrorCode);

            var mismatch = await Client.PostAsync(
                "/api/v1/auth/reset-password",
                new { email = user.Email, resetToken, newPassword = TestApi.DefaultPassword, confirmPassword = "Different#Passw0rd1" });
            Assert.Equal("PASSWORD_MISMATCH", mismatch.ErrorCode);

            var skip = _api.Emails.Count;
            var reset = await Client.PostAsync(
                "/api/v1/auth/reset-password",
                new { email = user.Email, resetToken, newPassword = TestApi.DefaultPassword, confirmPassword = TestApi.DefaultPassword });
            Assert.Equal(200, reset.Code);

            var changed = await _api.Emails.WaitForAsync(
                m => m.To == user.Email && m.Subject.Contains("password was changed", StringComparison.OrdinalIgnoreCase),
                skip, TimeSpan.FromSeconds(10));
            Assert.NotNull(changed);

            // The reset token cannot be used twice.
            var replay = await Client.PostAsync(
                "/api/v1/auth/reset-password",
                new { email = user.Email, resetToken, newPassword = "Another#Passw0rd9", confirmPassword = "Another#Passw0rd9" });
            Assert.Equal(400, replay.Code);
            Assert.Equal("RESET_TOKEN_INVALID", replay.ErrorCode);

            var login = await _api.LoginAsync(user.UserName, TestApi.DefaultPassword);
            Assert.Equal(200, login.Code);
        }

        [Fact]
        public async Task The_otp_is_locked_after_five_wrong_attempts()
        {
            var user = await _api.CreateUserAsync("otplock", setPassword: false);
            var otp = await _api.RequestOtpAsync(user.Email);
            var wrong = otp == "000000" ? "111111" : "000000";

            for (var attempt = 0; attempt < 5; attempt++)
            {
                var response = await Client.PostAsync("/api/v1/auth/verify-otp", new { email = user.Email, otp = wrong });
                Assert.Equal("OTP_INVALID", response.ErrorCode);
            }

            var correctButLocked = await Client.PostAsync("/api/v1/auth/verify-otp", new { email = user.Email, otp });
            Assert.Equal(400, correctButLocked.Code);
            Assert.Equal("OTP_INVALID", correctButLocked.ErrorCode);
        }

        [Fact]
        public async Task A_password_reset_revokes_every_session_and_the_old_password()
        {
            var user = await _api.CreateUserAsync("revoke");
            var session = await _api.LoginAsync(user.UserName, user.Password);
            Assert.Equal(200, session.Code);

            const string newPassword = "Brand#New1Passw0rd";
            await _api.SetPasswordAsync(user.Email, newPassword);

            var refresh = await Client.PostAsync("/api/v1/auth/refresh", new { refreshToken = session.Str("refreshToken") });
            Assert.Equal(401, refresh.Code);

            var oldPassword = await _api.LoginAsync(user.UserName, user.Password);
            Assert.Equal(401, oldPassword.Code);
            Assert.Equal("INVALID_CREDENTIALS", oldPassword.ErrorCode);

            var newLogin = await _api.LoginAsync(user.UserName, newPassword);
            Assert.Equal(200, newLogin.Code);
        }
    }

}
