using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.SignalR;
using Microsoft.CodeAnalysis;
using System.Reflection;
using UniCore.Api.Extensions;

namespace UniCore.ArchitectureTests
{
    // Week 23 endpoint review, kept as a rule: a new endpoint without protection fails the build.
    public class EndpointSecurityTests
    {
        private static readonly Assembly ApiAssembly = typeof(UniCore.Api.Program).Assembly;
        private static readonly Assembly InfrastructureAssembly = typeof(UniCore.Infrastructure.Common.SystemClock).Assembly;

        // The only endpoints that may be called without signing in.
        private static readonly HashSet<string> ApprovedAnonymous =
        [
            "AuthController.Login",
            "AuthController.Refresh",
            "AuthController.ForgotPassword",
            "AuthController.VerifyOtp",
            "AuthController.ResetPassword"
        ];

        private static IEnumerable<Type> Controllers() =>
            ApiAssembly.GetTypes().Where(t => t is { IsClass: true, IsAbstract: false } && typeof(ControllerBase).IsAssignableFrom(t));

        private static IEnumerable<MethodInfo> Actions() =>
            Controllers().SelectMany(c => c.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                .Where(m => m.IsDefined(typeof(HttpMethodAttribute), true));

        private static string Name(MethodInfo method) => $"{method.DeclaringType!.Name}.{method.Name}";

        private static bool IsAnonymous(MethodInfo method) =>
            method.IsDefined(typeof(AllowAnonymousAttribute), true) || method.DeclaringType!.IsDefined(typeof(AllowAnonymousAttribute), true);

        // HasPermissionAttribute derives from AuthorizeAttribute, so it counts.
        private static bool RequiresAuthorization(MethodInfo method) =>
            method.IsDefined(typeof(AuthorizeAttribute), true) || method.DeclaringType!.IsDefined(typeof(AuthorizeAttribute), true);

        private static bool IsUpload(MethodInfo method) =>
            method.GetCustomAttributes<ConsumesAttribute>(true).Any(c => c.ContentTypes.Contains("multipart/form-data"));

        [Fact]
        public void Controllers_and_actions_are_found()
        {
            Assert.NotEmpty(Controllers());
            Assert.NotEmpty(Actions());
        }

        [Fact]
        public void Every_action_requires_authorization_or_is_explicitly_anonymous()
        {
            var unprotected = Actions().Where(m => !IsAnonymous(m) && !RequiresAuthorization(m)).Select(Name).ToList();

            Assert.True(unprotected.Count == 0, "Add [Authorize], [HasPermission] or an explicit [AllowAnonymous] to: " + string.Join(", ", unprotected));
        }

        [Fact]
        public void Anonymous_actions_are_exactly_the_approved_list()
        {
            var actual = Actions().Where(IsAnonymous).Select(Name).ToHashSet();

            Assert.True(actual.SetEquals(ApprovedAnonymous),
                "Anonymous actions changed. Unexpected: [" + string.Join(", ", actual.Except(ApprovedAnonymous)) +
                "] Missing: [" + string.Join(", ", ApprovedAnonymous.Except(actual)) + "]");
        }

        [Fact]
        public void Anonymous_actions_are_rate_limited()
        {
            var unlimited = Actions().Where(IsAnonymous).Where(m => !m.IsDefined(typeof(EnableRateLimitingAttribute), true)).Select(Name).ToList();

            Assert.True(unlimited.Count == 0, "Add [EnableRateLimiting] to: " + string.Join(", ", unlimited));
        }

        [Fact]
        public void Upload_actions_have_their_own_size_and_rate_limit()
        {
            var uploads = Actions().Where(IsUpload).ToList();
            Assert.NotEmpty(uploads);

            var wrong = uploads.Where(m =>
                    !m.IsDefined(typeof(RequestSizeLimitAttribute), true) ||
                    m.GetCustomAttributes<EnableRateLimitingAttribute>(true).All(a => a.PolicyName != RateLimitPolicies.Upload))
                .Select(Name).ToList();

            Assert.True(wrong.Count == 0, "Upload actions need [RequestSizeLimit] and [EnableRateLimiting(RateLimitPolicies.Upload)]: " + string.Join(", ", wrong));
        }

        [Fact]
        public void Controllers_use_ApiController_and_the_versioned_route()
        {
            var wrong = Controllers().Where(t =>
                    !t.IsDefined(typeof(ApiControllerAttribute), true) ||
                    !(t.GetCustomAttribute<RouteAttribute>()?.Template ?? string.Empty).StartsWith("api/v1/", StringComparison.Ordinal))
                .Select(t => t.Name).ToList();

            Assert.True(wrong.Count == 0, "These controllers need [ApiController] and a route starting with api/v1/: " + string.Join(", ", wrong));
        }

        [Fact]
        public void Hubs_require_authorization()
        {
            var hubs = ApiAssembly.GetTypes().Concat(InfrastructureAssembly.GetTypes()).Where(t => t is { IsClass: true, IsAbstract: false } && typeof(Hub).IsAssignableFrom(t)).ToList();

            Assert.NotEmpty(hubs);

            var open = hubs.Where(t => !t.IsDefined(typeof(AuthorizeAttribute), true)).Select(t => t.Name).ToList();

            Assert.True(open.Count == 0, "Add [Authorize] to: " + string.Join(", ", open));
        }
    }
}
