using UniCore.Application.Common.Abstractions;
using UniCore.Application.Modules.Identity;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Identity;

namespace UniCore.UnitTests.Modules.Identity
{
    public class PermissionAuthorizerTests
    {
        private static readonly Guid Faculty = Guid.NewGuid();
        private static readonly Guid OtherFaculty = Guid.NewGuid();
        private static readonly Guid Department = Guid.NewGuid();
        private static readonly Guid Section = Guid.NewGuid();

        private static RbacTestEnv SignedIn(params RoleGrant[] grants)
        {
            var env = new RbacTestEnv();
            env.SignIn(Guid.NewGuid(), grants);
            return env;
        }

        private static RoleGrant Dean(params ScopeGrant[] scopes) => TestGrants.Role(RoleNames.Dean, [Permissions.Grades.Approve, Permissions.Grades.Read], scopes);

        [Fact]
        public async Task Anonymous_caller_is_unauthorized()
        {
            var env = new RbacTestEnv();

            var result = await env.Authorizer.AuthorizeAsync(Permissions.Grades.Read);

            Assert.Equal(ErrorType.Unauthorized, result.Error?.ErrorType);
        }

        [Fact]
        public async Task Signed_in_caller_without_loaded_access_is_forbidden()
        {
            var env = new RbacTestEnv();
            env.User.UserId = Guid.NewGuid(); // authenticated, but no roles were loaded

            var result = await env.Authorizer.AuthorizeAsync(Permissions.Grades.Read);

            Assert.Equal("FORBIDDEN_PERMISSION_MISSING", result.Error?.Code);
        }

        [Fact]
        public async Task Missing_permission_is_forbidden_even_for_a_matching_scope()
        {
            var env = SignedIn(Dean(new ScopeGrant(ScopeType.Faculty, Faculty)));

            var result = await env.Authorizer.AuthorizeAsync(
                Permissions.Grades.Publish, ResourceContext.InScope(ScopeType.Faculty, Faculty));

            Assert.Equal("FORBIDDEN_PERMISSION_MISSING", result.Error?.Code);
            Assert.Equal(ErrorType.Forbidden, result.Error?.ErrorType);
        }

        [Fact]
        public async Task Permission_without_a_resource_is_enough()
        {
            var env = SignedIn(Dean());

            var result = await env.Authorizer.AuthorizeAsync(Permissions.Grades.Approve);

            Assert.True(result.IsSuccess);
        }

        [Fact]
        public async Task Owner_of_the_resource_passes_without_any_scope()
        {
            var env = SignedIn(TestGrants.Role(RoleNames.Student, new[] { Permissions.Grades.Read }));
            var me = env.User.UserId!.Value;

            var result = await env.Authorizer.AuthorizeAsync(Permissions.Grades.Read, ResourceContext.OwnedBy(me));

            Assert.True(result.IsSuccess);
        }

        [Fact]
        public async Task Student_cannot_read_someone_elses_record()
        {
            var env = SignedIn(TestGrants.Role(RoleNames.Student, new[] { Permissions.Grades.Read }));

            var result = await env.Authorizer.AuthorizeAsync(
                Permissions.Grades.Read, ResourceContext.OwnedBy(Guid.NewGuid()));

            Assert.Equal("FORBIDDEN_NOT_OWNER", result.Error?.Code);
        }

        [Fact]
        public async Task Empty_owner_list_counts_as_no_ownership_information()
        {
            var env = SignedIn(TestGrants.Role(RoleNames.Student, new[] { Permissions.Grades.Read }));

            var result = await env.Authorizer.AuthorizeAsync(Permissions.Grades.Read, ResourceContext.OwnedBy());

            Assert.Equal("FORBIDDEN_OUT_OF_SCOPE", result.Error?.Code);
        }

        [Fact]
        public async Task Global_scope_covers_every_resource()
        {
            var env = SignedIn(Dean(new ScopeGrant(ScopeType.Global, null)));

            Assert.True((await env.Authorizer.AuthorizeAsync(Permissions.Grades.Approve, ResourceContext.InScope(ScopeType.Section, Section))).IsSuccess);
            Assert.True((await env.Authorizer.AuthorizeAsync(Permissions.Grades.Approve, ResourceContext.InScope(ScopeType.Global, Guid.Empty))).IsSuccess);
        }

        [Fact]
        public async Task Super_admin_passes_everything()
        {
            var env = SignedIn(TestGrants.SuperAdmin());

            var result = await env.Authorizer.AuthorizeAsync(
                Permissions.Settings.Manage, ResourceContext.InScope(ScopeType.Faculty, Faculty));

            Assert.True(result.IsSuccess);
        }

        [Fact]
        public async Task Faculty_scope_matches_only_that_faculty()
        {
            var env = SignedIn(Dean(new ScopeGrant(ScopeType.Faculty, Faculty)));

            var same = await env.Authorizer.AuthorizeAsync(
                Permissions.Grades.Approve, ResourceContext.InScope(ScopeType.Faculty, Faculty));
            var otherId = await env.Authorizer.AuthorizeAsync(
                Permissions.Grades.Approve, ResourceContext.InScope(ScopeType.Faculty, OtherFaculty));
            var otherType = await env.Authorizer.AuthorizeAsync(
                Permissions.Grades.Approve, ResourceContext.InScope(ScopeType.Department, Faculty));

            Assert.True(same.IsSuccess);
            Assert.Equal("FORBIDDEN_OUT_OF_SCOPE", otherId.Error?.Code);
            Assert.Equal("FORBIDDEN_OUT_OF_SCOPE", otherType.Error?.Code);
        }

        [Fact]
        public async Task Scope_covers_resources_below_it_when_the_hierarchy_says_so()
        {
            var env = SignedIn(Dean(new ScopeGrant(ScopeType.Faculty, Faculty)));
            env.Hierarchy.Chains[(ScopeType.Section, Section)] = new[]
            {
            new ScopeRef(ScopeType.Section, Section),
            new ScopeRef(ScopeType.Department, Department),
            new ScopeRef(ScopeType.Faculty, Faculty)
        };

            var result = await env.Authorizer.AuthorizeAsync(
                Permissions.Grades.Approve, ResourceContext.InScope(ScopeType.Section, Section));

            Assert.True(result.IsSuccess);
        }

        [Fact]
        public async Task Scope_does_not_cover_a_section_that_belongs_to_another_faculty()
        {
            var env = SignedIn(Dean(new ScopeGrant(ScopeType.Faculty, Faculty)));
            env.Hierarchy.Chains[(ScopeType.Section, Section)] = new[]
            {
            new ScopeRef(ScopeType.Section, Section),
            new ScopeRef(ScopeType.Faculty, OtherFaculty)
        };

            var result = await env.Authorizer.AuthorizeAsync(
                Permissions.Grades.Approve, ResourceContext.InScope(ScopeType.Section, Section));

            Assert.Equal("FORBIDDEN_OUT_OF_SCOPE", result.Error?.Code);
        }

        [Fact]
        public async Task A_scope_only_counts_for_the_role_that_grants_the_permission()
        {
            // Role A has the permission but no scope. Role B has a Global scope but not the permission.
            var env = SignedIn(
                TestGrants.Role("A", new[] { Permissions.Grades.Approve }),
                TestGrants.Role("B", new[] { Permissions.Grades.Read }, new ScopeGrant(ScopeType.Global, null)));

            var result = await env.Authorizer.AuthorizeAsync(
                Permissions.Grades.Approve, ResourceContext.InScope(ScopeType.Faculty, Faculty));

            Assert.Equal("FORBIDDEN_OUT_OF_SCOPE", result.Error?.Code);
        }

        [Fact]
        public async Task Another_role_with_the_permission_and_the_right_scope_is_enough()
        {
            var env = SignedIn(
                TestGrants.Role("A", new[] { Permissions.Grades.Approve }),
                TestGrants.Role("B", new[] { Permissions.Grades.Approve }, new ScopeGrant(ScopeType.Faculty, Faculty)));

            var result = await env.Authorizer.AuthorizeAsync(
                Permissions.Grades.Approve, ResourceContext.InScope(ScopeType.Faculty, Faculty));

            Assert.True(result.IsSuccess);
        }

        [Fact]
        public async Task A_global_resource_needs_a_global_grant()
        {
            var env = SignedIn(Dean(new ScopeGrant(ScopeType.Faculty, Faculty)));

            var result = await env.Authorizer.AuthorizeAsync(
                Permissions.Grades.Approve, ResourceContext.InScope(ScopeType.Global, Guid.Empty));

            Assert.Equal("FORBIDDEN_OUT_OF_SCOPE", result.Error?.Code);
        }

        [Fact]
        public async Task A_resource_with_a_scope_type_but_no_id_fails_closed()
        {
            var env = SignedIn(Dean(new ScopeGrant(ScopeType.Faculty, Faculty)));

            var result = await env.Authorizer.AuthorizeAsync(
                Permissions.Grades.Approve, new ResourceContext(ScopeType.Faculty, null));

            Assert.Equal("FORBIDDEN_OUT_OF_SCOPE", result.Error?.Code);
        }

        [Fact]
        public async Task Owner_and_scope_can_be_combined_either_one_is_enough()
        {
            var env = SignedIn(Dean(new ScopeGrant(ScopeType.Faculty, Faculty)));
            var me = env.User.UserId!.Value;

            var byOwnership = await env.Authorizer.AuthorizeAsync(
                Permissions.Grades.Approve,
                ResourceContext.InScope(ScopeType.Faculty, OtherFaculty).WithOwners(me));

            var byScope = await env.Authorizer.AuthorizeAsync(
                Permissions.Grades.Approve,
                ResourceContext.InScope(ScopeType.Faculty, Faculty).WithOwners(Guid.NewGuid()));

            Assert.True(byOwnership.IsSuccess);
            Assert.True(byScope.IsSuccess);
        }
    }

}
