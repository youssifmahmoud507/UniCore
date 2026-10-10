using Microsoft.AspNetCore.Http;
using System.Security.Claims;
using UniCore.Application.Modules.Identity;
using UniCore.Infrastructure.Modules.Identity;

namespace UniCore.IntegrationTests.Identity
{
    public class CurrentUserTests
    {
        private static CurrentUser Build(ClaimsPrincipal? principal)
        {
            var context = new DefaultHttpContext();
            if (principal is not null)
            {
                context.User = principal;
            }

            return new CurrentUser(new HttpContextAccessor { HttpContext = context });
        }

        private static ClaimsPrincipal Authenticated(params Claim[] claims)
            => new(new ClaimsIdentity(claims, "test", AppClaimTypes.Name, AppClaimTypes.Role));

        [Fact]
        public void Authenticated_user_exposes_ids_and_roles()
        {
            var userId = Guid.NewGuid();
            var personId = Guid.NewGuid();

            var user = Build(Authenticated(
                new Claim(AppClaimTypes.Sub, userId.ToString()),
                new Claim(AppClaimTypes.PersonId, personId.ToString()),
                new Claim(AppClaimTypes.Role, "Student"),
                new Claim(AppClaimTypes.Role, "Instructor")));

            Assert.True(user.IsAuthenticated);
            Assert.Equal(userId, user.UserId);
            Assert.Equal(personId, user.PersonId);
            Assert.Equal(2, user.Roles.Count);
            Assert.True(user.IsInRole("Student"));
            Assert.False(user.IsInRole("Dean"));
        }

        [Fact]
        public void Anonymous_user_has_nothing()
        {
            var user = Build(null);

            Assert.False(user.IsAuthenticated);
            Assert.Null(user.UserId);
            Assert.Null(user.PersonId);
            Assert.Empty(user.Roles);
            Assert.False(user.IsInRole("Student"));
        }

        [Fact]
        public void Missing_http_context_is_treated_as_anonymous()
        {
            var user = new CurrentUser(new HttpContextAccessor());

            Assert.False(user.IsAuthenticated);
            Assert.Null(user.UserId);
        }

        [Fact]
        public void Malformed_guid_claims_give_null_instead_of_failing()
        {
            var user = Build(Authenticated(new Claim(AppClaimTypes.Sub, "not-a-guid")));

            Assert.True(user.IsAuthenticated);
            Assert.Null(user.UserId);
        }

        [Fact]
        public void Permissions_are_empty_until_the_authorization_batch()
        {
            var user = Build(Authenticated(new Claim(AppClaimTypes.Sub, Guid.NewGuid().ToString())));

            Assert.False(user.HasPermission("Students.Read"));
            Assert.Empty(user.Scopes);
        }
    }
}
