using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Audit;

namespace UniCore.UnitTests.Modules.Identity
{
    public class PasswordResetHandlersTests
    {
        // ---------- forgot-password ----------

        [Fact]
        public async Task Forgot_for_an_unknown_email_answers_ok_and_stores_and_sends_nothing()
        {
            var env = new PasswordResetTestEnv();

            var result = await env.ForgotAsync("nobody@uni.edu");

            Assert.True(result.IsSuccess);
            Assert.Empty(env.Otps.Items);
            Assert.Empty(env.Emails.Messages);
        }

        [Fact]
        public async Task Forgot_for_a_known_email_stores_only_a_hash_and_queues_the_email()
        {
            var env = new PasswordResetTestEnv();

            var result = await env.ForgotAsync();

            Assert.True(result.IsSuccess);
            var stored = Assert.Single(env.Otps.Items);
            Assert.Equal($"h:{env.UserId:N}:123456", stored.OtpHash);
            Assert.NotEqual("123456", stored.OtpHash);
            Assert.Equal(AuthTestEnv.Start.AddMinutes(10), stored.ExpiresAt);
            Assert.Equal(AuthTestEnv.Start.AddSeconds(60), stored.ResendAvailableAt);
            Assert.Equal(5, stored.MaxAttempts);

            var email = Assert.Single(env.Emails.Messages);
            Assert.Equal(PasswordResetTestEnv.Email, email.To);
            Assert.Contains("123456", email.TextBody);
            Assert.Contains("123456", email.HtmlBody);
            Assert.Contains("10 minutes", email.TextBody);
            Assert.Contains(env.Audit.Entries, e => e.Action == AuditActions.AuthPasswordResetRequested);
        }

        [Fact]
        public async Task Forgot_answers_identically_for_known_and_unknown_emails()
        {
            var env = new PasswordResetTestEnv();

            var known = await env.ForgotAsync();
            var unknown = await env.ForgotAsync("nobody@uni.edu");

            Assert.Equal(known.IsSuccess, unknown.IsSuccess);
            Assert.Equal(known.Errors.Count, unknown.Errors.Count);
        }

        [Fact]
        public async Task Forgot_inside_the_cooldown_does_not_create_another_otp_or_email()
        {
            var env = new PasswordResetTestEnv();
            await env.ForgotAsync();

            env.Clock.Advance(TimeSpan.FromSeconds(30));
            var again = await env.ForgotAsync();

            Assert.True(again.IsSuccess);
            Assert.Single(env.Otps.Items);
            Assert.Single(env.Emails.Messages);
            Assert.Contains(env.Audit.Entries, e => e.Action == AuditActions.AuthPasswordResetThrottled);
        }

        [Fact]
        public async Task Forgot_after_the_cooldown_invalidates_the_previous_otp()
        {
            var env = new PasswordResetTestEnv();
            await env.ForgotAsync();

            env.Clock.Advance(TimeSpan.FromSeconds(61));
            await env.ForgotAsync();

            Assert.Equal(2, env.Otps.Items.Count);
            Assert.Equal(1, env.Otps.Items.Count(o => o.IsUsable(env.Clock.UtcNow)));
            Assert.Equal(2, env.Emails.Messages.Count);
        }

        [Fact]
        public async Task Forgot_stops_at_the_hourly_cap_but_still_answers_ok()
        {
            var env = new PasswordResetTestEnv();
            env.Settings.MaxRequestsPerHour = 2;

            for (var i = 0; i < 3; i++)
            {
                var result = await env.ForgotAsync();
                Assert.True(result.IsSuccess);
                env.Clock.Advance(TimeSpan.FromSeconds(61));
            }

            Assert.Equal(2, env.Otps.Items.Count);
            Assert.Equal(2, env.Emails.Messages.Count);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("not-an-email")]
        public async Task Forgot_with_a_malformed_email_is_a_validation_error(string? email)
        {
            var env = new PasswordResetTestEnv();

            var result = await env.Forgot.HandleAsync(new UniCore.Application.Modules.Identity.ForgotPasswordCommand(email, null, null));

            Assert.Equal("USER_EMAIL_INVALID", result.Error?.Code);
        }

        [Fact]
        public async Task Forgot_when_saving_fails_still_answers_ok_and_sends_no_email()
        {
            var env = new PasswordResetTestEnv();
            env.Uow.FailWith = CommonErrors.DatabaseUpdateFailed;

            var result = await env.ForgotAsync();

            Assert.True(result.IsSuccess);
            Assert.Empty(env.Emails.Messages);
            Assert.Contains(env.Audit.Entries, e => e.Action == AuditActions.AuthPasswordResetRequestFailed);
        }

        [Fact]
        public async Task Forgot_when_the_email_queue_is_full_still_answers_ok_and_audits_it()
        {
            var env = new PasswordResetTestEnv();
            env.Emails.Full = true;

            var result = await env.ForgotAsync();

            Assert.True(result.IsSuccess);
            Assert.Contains(env.Audit.Entries, e => e.Action == AuditActions.EmailEnqueueFailed);
        }

        // ---------- verify-otp ----------

        [Fact]
        public async Task Verify_with_the_right_code_returns_a_reset_token_and_consumes_the_otp()
        {
            var env = new PasswordResetTestEnv();
            await env.ForgotAsync();

            var result = await env.VerifyAsync("123456");

            Assert.True(result.TryGetValue(out var response));
            Assert.NotNull(response);
            Assert.Equal("reset-token", response.ResetToken);
            Assert.Equal(AuthTestEnv.Start.AddMinutes(10), response.ExpiresAt);
            Assert.True(Assert.Single(env.Otps.Items).IsConsumed);
            Assert.Contains(env.Audit.Entries, e => e.Action == AuditActions.AuthOtpVerified);
        }

        [Fact]
        public async Task Verify_cannot_be_used_twice_with_the_same_code()
        {
            var env = new PasswordResetTestEnv();
            await env.ForgotAsync();
            await env.VerifyAsync("123456");

            var second = await env.VerifyAsync("123456");

            Assert.True(second.IsFailure);
            Assert.Equal("OTP_INVALID", second.Error?.Code);
        }

        [Fact]
        public async Task Verify_with_a_wrong_code_counts_the_attempt()
        {
            var env = new PasswordResetTestEnv();
            await env.ForgotAsync();

            var result = await env.VerifyAsync("000000");

            Assert.Equal("OTP_INVALID", result.Error?.Code);
            Assert.Equal(1, Assert.Single(env.Otps.Items).AttemptCount);
            Assert.Contains(env.Audit.Entries, e => e.Action == AuditActions.AuthOtpFailed);
        }

        [Fact]
        public async Task Verify_locks_the_otp_after_the_maximum_wrong_attempts()
        {
            var env = new PasswordResetTestEnv();
            await env.ForgotAsync();

            for (var i = 0; i < 5; i++)
            {
                var wrong = await env.VerifyAsync("000000");
                Assert.Equal("OTP_INVALID", wrong.Error?.Code);
            }

            var correctButLocked = await env.VerifyAsync("123456");

            Assert.True(correctButLocked.IsFailure);
            Assert.Equal("OTP_INVALID", correctButLocked.Error?.Code);
            Assert.Contains(env.Audit.Entries, e => e.Action == AuditActions.AuthOtpAttemptsExceeded);
            Assert.False(Assert.Single(env.Otps.Items).IsConsumed);
        }

        [Fact]
        public async Task Verify_with_an_expired_code_fails_and_audits_the_reason()
        {
            var env = new PasswordResetTestEnv();
            await env.ForgotAsync();

            env.Clock.Advance(TimeSpan.FromMinutes(11));
            var result = await env.VerifyAsync("123456");

            Assert.Equal("OTP_INVALID", result.Error?.Code);
            Assert.Contains(env.Audit.Entries, e => e.Action == AuditActions.AuthOtpExpired);
        }

        [Fact]
        public async Task Verify_for_an_unknown_email_gives_the_same_error_as_a_wrong_code()
        {
            var env = new PasswordResetTestEnv();
            await env.ForgotAsync();

            var unknown = await env.VerifyAsync("123456", "nobody@uni.edu");
            var wrong = await env.VerifyAsync("000000");

            Assert.Equal(wrong.Error?.Code, unknown.Error?.Code);
            Assert.Equal(wrong.Error?.Description, unknown.Error?.Description);
        }

        [Theory]
        [InlineData("12345")]
        [InlineData("1234567")]
        [InlineData("12a456")]
        [InlineData("")]
        [InlineData(null)]
        public async Task Verify_rejects_malformed_codes_without_touching_the_otp(string? otp)
        {
            var env = new PasswordResetTestEnv();
            await env.ForgotAsync();

            var result = await env.Verify.HandleAsync(
                new UniCore.Application.Modules.Identity.VerifyOtpCommand(PasswordResetTestEnv.Email, otp, null, null));

            Assert.Equal("OTP_INVALID", result.Error?.Code);
            Assert.Equal(0, Assert.Single(env.Otps.Items).AttemptCount);
        }

        // ---------- reset-password ----------

        [Fact]
        public async Task Reset_changes_the_password_revokes_every_session_and_sends_the_confirmation()
        {
            var env = new PasswordResetTestEnv();
            await env.ForgotAsync();
            var sessionA = env.AddActiveRefreshToken();
            var sessionB = env.AddActiveRefreshToken();

            var result = await env.ResetAsync();

            Assert.True(result.IsSuccess);
            Assert.Equal(1, env.Accounts.ApplyCalls);
            Assert.True(sessionA.IsRevoked);
            Assert.True(sessionB.IsRevoked);
            Assert.All(env.Otps.Items, o => Assert.False(o.IsUsable(env.Clock.UtcNow)));
            Assert.Contains(env.Emails.Messages, m => m.Subject.Contains("password was changed"));
            Assert.Contains(env.Audit.Entries, e => e.Action == AuditActions.AuthPasswordResetCompleted);
        }

        [Fact]
        public async Task Reset_with_different_passwords_changes_nothing()
        {
            var env = new PasswordResetTestEnv();
            var session = env.AddActiveRefreshToken();

            var result = await env.ResetAsync(newPassword: "NewP@ssw0rd!!", confirm: "Other");

            Assert.Equal("PASSWORD_MISMATCH", result.Error?.Code);
            Assert.Equal(0, env.Accounts.ApplyCalls);
            Assert.False(session.IsRevoked);
        }

        [Fact]
        public async Task Reset_with_an_invalid_token_changes_nothing()
        {
            var env = new PasswordResetTestEnv();
            var session = env.AddActiveRefreshToken();
            env.Accounts.ValidateResult = Result.Fail(UniCore.Domain.Modules.Identity.IdentityErrors.ResetTokenInvalid);

            var result = await env.ResetAsync();

            Assert.Equal("RESET_TOKEN_INVALID", result.Error?.Code);
            Assert.Equal(0, env.Accounts.ApplyCalls);
            Assert.False(session.IsRevoked);
            Assert.Empty(env.Emails.Messages);
        }

        [Fact]
        public async Task Reset_with_a_weak_password_changes_nothing()
        {
            var env = new PasswordResetTestEnv();
            var session = env.AddActiveRefreshToken();
            env.Accounts.ValidateResult = Result.Fail(UniCore.Domain.Modules.Identity.IdentityErrors.PasswordPolicyViolation);

            var result = await env.ResetAsync();

            Assert.Equal("PASSWORD_POLICY_VIOLATION", result.Error?.Code);
            Assert.False(session.IsRevoked);
            Assert.Equal(0, env.Accounts.ApplyCalls);
        }

        [Fact]
        public async Task Reset_for_an_unknown_email_looks_like_an_invalid_token()
        {
            var env = new PasswordResetTestEnv();

            var result = await env.ResetAsync(email: "nobody@uni.edu");

            Assert.Equal("RESET_TOKEN_INVALID", result.Error?.Code);
        }

        [Fact]
        public async Task Reset_never_changes_the_password_when_revoking_the_sessions_fails()
        {
            var env = new PasswordResetTestEnv();
            env.AddActiveRefreshToken();
            env.Uow.FailWith = CommonErrors.DatabaseUpdateFailed;

            var result = await env.ResetAsync();

            Assert.True(result.IsFailure);
            Assert.Equal(0, env.Accounts.ApplyCalls);
        }
    }



}
