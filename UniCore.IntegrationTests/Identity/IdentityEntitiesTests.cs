using Microsoft.AspNetCore.Identity;
using System;
using System.Collections.Generic;
using System.Text;
using UniCore.Infrastructure.Modules.Identity;

namespace UniCore.IntegrationTests.Identity
{
    public class IdentityEntitiesTests
    {
        private static readonly DateTimeOffset Now = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

        [Fact]
        public void User_Create_with_valid_data_succeeds_and_is_active()
        {
            var result = ApplicationUser.Create("  ahmed  ", "ahmed@uni.edu", " 01000000000 ", Now);

            Assert.True(result.TryGetValue(out var user));
            Assert.NotNull(user);
            Assert.Equal("ahmed", user.UserName);
            Assert.Equal("ahmed@uni.edu", user.Email);
            Assert.Equal("01000000000", user.PhoneNumber);
            Assert.True(user.IsActive);
            Assert.False(user.EmailConfirmed);
            Assert.Equal(Now, user.CreatedAt);
            Assert.Null(user.LastLoginAt);
            Assert.Null(user.UpdatedAt);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void User_Create_without_username_fails(string? userName)
        {
            var result = ApplicationUser.Create(userName, "a@b.com", null, Now);

            Assert.True(result.IsFailure);
            Assert.Equal("USER_NAME_REQUIRED", result.Error?.Code);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("not-an-email")]
        [InlineData("two@@signs.com")]
        [InlineData("has space@uni.edu")]
        public void User_Create_with_invalid_email_fails(string? email)
        {
            var result = ApplicationUser.Create("ahmed", email, null, Now);

            Assert.True(result.IsFailure);
            Assert.Equal("USER_EMAIL_INVALID", result.Error?.Code);
        }

        [Fact]
        public void User_Activate_Deactivate_and_RecordLogin_update_state()
        {
            var user = ApplicationUser.Create("ahmed", "ahmed@uni.edu", null, Now).Value;
            Assert.NotNull(user);

            var later = Now.AddHours(1);

            user.Deactivate(later);
            Assert.False(user.IsActive);
            Assert.Equal(later, user.UpdatedAt);

            user.Activate(later.AddHours(1));
            Assert.True(user.IsActive);

            user.RecordLogin(later);
            Assert.Equal(later, user.LastLoginAt);
        }

        [Fact]
        public void Role_Create_trims_the_name()
        {
            var result = ApplicationRole.Create("  Dean ");

            Assert.True(result.TryGetValue(out var role));
            Assert.NotNull(role);
            Assert.Equal("Dean", role.Name);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("  ")]
        public void Role_Create_without_name_fails(string? name)
        {
            var result = ApplicationRole.Create(name);

            Assert.True(result.IsFailure);
            Assert.Equal("ROLE_NAME_REQUIRED", result.Error?.Code);
        }

        [Fact]
        public void IdentityResult_success_maps_to_Ok()
        {
            Assert.True(IdentityResult.Success.ToResult().IsSuccess);
        }

        [Theory]
        [InlineData("DuplicateUserName", "USER_ALREADY_EXISTS")]
        [InlineData("DuplicateEmail", "USER_ALREADY_EXISTS")]
        [InlineData("DuplicateRoleName", "ROLE_ALREADY_EXISTS")]
        [InlineData("PasswordTooShort", "PASSWORD_POLICY_VIOLATION")]
        [InlineData("PasswordRequiresDigit", "PASSWORD_POLICY_VIOLATION")]
        [InlineData("InvalidEmail", "USER_EMAIL_INVALID")]
        [InlineData("ConcurrencyFailure", "CONCURRENCY_CONFLICT")]
        [InlineData("SomethingUnknown", "IDENTITY_OPERATION_FAILED")]
        public void IdentityResult_failures_map_to_our_error_codes(string identityCode, string expectedCode)
        {
            var failed = IdentityResult.Failed(new IdentityError { Code = identityCode, Description = "raw description" });

            var result = failed.ToResult();

            Assert.True(result.IsFailure);
            Assert.Equal(expectedCode, result.Error?.Code);
        }

        [Fact]
        public void Unknown_identity_errors_do_not_leak_the_raw_description()
        {
            var failed = IdentityResult.Failed(new IdentityError { Code = "SomethingUnknown", Description = "secret internals" });

            var result = failed.ToResult();

            Assert.DoesNotContain("secret internals", result.Error?.Description);
        }
    }
}
