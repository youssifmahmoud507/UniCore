using UniCore.Application.Common.Abstractions;
using UniCore.Application.Modules.Identity;
using UniCore.Application.Modules.Reporting;
using UniCore.Domain.Modules.Identity;

namespace UniCore.UnitTests.Modules.Reporting
{
    public class ReportScopeResolverTests
    {
        private static readonly Guid Faculty = Guid.NewGuid();
        private static readonly Guid OtherFaculty = Guid.NewGuid();
        private static readonly Guid Department = Guid.NewGuid();
        private static readonly Guid OtherDepartment = Guid.NewGuid();
        private static readonly Guid Section = Guid.NewGuid();

        private static ReportingEnv Env()
        {
            var env = new ReportingEnv();

            // Section -> Department -> Faculty, and a department of another faculty.
            env.Hierarchy.Chains[(ScopeType.Section, Section)] = [new ScopeRef(ScopeType.Section, Section), new ScopeRef(ScopeType.Department, Department), new ScopeRef(ScopeType.Faculty, Faculty)];
            env.Hierarchy.Chains[(ScopeType.Department, Department)] = [new ScopeRef(ScopeType.Department, Department), new ScopeRef(ScopeType.Faculty, Faculty)];
            env.Hierarchy.Chains[(ScopeType.Department, OtherDepartment)] = [new ScopeRef(ScopeType.Department, OtherDepartment), new ScopeRef(ScopeType.Faculty, OtherFaculty)];

            return env;
        }

        private static ReportScopeResolver Resolver(ReportingEnv env) => new(env.Access, env.Hierarchy);

        private static ScopeRef[] DataScopes() =>
        [
            new(ScopeType.Section, Section),
            new(ScopeType.Department, Department),
            new(ScopeType.Department, OtherDepartment)
        ];

        [Fact]
        public async Task A_global_grant_sees_everything()
        {
            var env = Env().Grant([Permissions.Requests.Read]);

            var scope = await Resolver(env).ResolveAsync(Permissions.Requests.Read, DataScopes());

            Assert.True(scope.IsGlobal);
            Assert.True(scope.Allows(Guid.NewGuid()));
            Assert.True(scope.Allows(null));
        }

        [Fact]
        public async Task A_faculty_grant_covers_its_departments_and_sections_and_nothing_else()
        {
            var env = Env().Grant([Permissions.Requests.Read], new ScopeGrant(ScopeType.Faculty, Faculty));

            var scope = await Resolver(env).ResolveAsync(Permissions.Requests.Read, DataScopes());

            Assert.False(scope.IsGlobal);
            Assert.True(scope.Allows(Section));
            Assert.True(scope.Allows(Department));
            Assert.False(scope.Allows(OtherDepartment));
        }

        [Fact]
        public async Task A_department_grant_does_not_cover_the_faculty_above_it_or_a_sibling_department()
        {
            var env = Env().Grant([Permissions.Requests.Read], new ScopeGrant(ScopeType.Department, Department));

            var scope = await Resolver(env).ResolveAsync(Permissions.Requests.Read, [.. DataScopes(), new ScopeRef(ScopeType.Faculty, Faculty)]);

            Assert.True(scope.Allows(Department));
            Assert.True(scope.Allows(Section));
            Assert.False(scope.Allows(OtherDepartment));
            Assert.False(scope.Allows(Faculty));
        }

        [Fact]
        public async Task Data_that_sits_in_no_scope_is_for_global_users_only()
        {
            var env = Env().Grant([Permissions.Requests.Read], new ScopeGrant(ScopeType.Faculty, Faculty));

            var scope = await Resolver(env).ResolveAsync(Permissions.Requests.Read, DataScopes());

            Assert.False(scope.Allows(null));
        }

        [Fact]
        public async Task Only_the_grants_that_hold_the_permission_count()
        {
            var env = Env().Grant([Permissions.Finance.Read], new ScopeGrant(ScopeType.Faculty, Faculty));

            var scope = await Resolver(env).ResolveAsync(Permissions.Requests.Read, DataScopes());

            Assert.False(scope.IsGlobal);
            Assert.False(scope.Allows(Department));
        }

        [Fact]
        public async Task A_user_with_no_access_sees_nothing()
        {
            var scope = await Resolver(Env()).ResolveAsync(Permissions.Requests.Read, DataScopes());

            Assert.Equal(ReportScope.Nothing, scope);
            Assert.False(scope.Allows(Department));
        }
    }




}
