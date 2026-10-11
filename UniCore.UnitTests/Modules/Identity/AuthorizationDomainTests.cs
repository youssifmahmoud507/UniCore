using UniCore.Domain.Modules.Identity;

namespace UniCore.UnitTests.Modules.Identity
{
    public class AuthorizationDomainTests
    {
        private static readonly DateTimeOffset Now = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

        // ---------- the catalog ----------

        [Fact]
        public void Every_catalog_entry_is_a_valid_permission_and_codes_are_unique()
        {
            Assert.Equal(Permissions.All.Count, Permissions.AllCodes.Count);

            foreach (var definition in Permissions.All)
            {
                var created = Permission.Create(definition.Code, definition.Code, definition.Description, definition.Module);

                Assert.True(created.IsSuccess, $"Invalid catalog entry: {definition.Code}");
            }
        }

        [Fact]
        public void Default_role_permissions_only_use_codes_from_the_catalog()
        {
            foreach (var (role, codes) in DefaultRolePermissions.ByRole)
            {
                Assert.NotEmpty(codes);
                Assert.Equal(codes.Count, codes.Distinct().Count());

                foreach (var code in codes)
                {
                    Assert.True(Permissions.AllCodes.Contains(code), $"{role} uses unknown code {code}");
                }
            }
        }

        [Fact]
        public void Default_roles_are_built_in_roles_and_super_admin_is_not_listed()
        {
            Assert.DoesNotContain(RoleNames.SuperAdmin, DefaultRolePermissions.ByRole.Keys);

            foreach (var role in DefaultRolePermissions.ByRole.Keys)
            {
                Assert.Contains(role, RoleNames.All);
            }
        }

        [Fact]
        public void Dean_holds_everything_a_department_head_holds()
        {
            var head = DefaultRolePermissions.ByRole[RoleNames.DepartmentHead];
            var dean = DefaultRolePermissions.ByRole[RoleNames.Dean];

            Assert.All(head, code => Assert.Contains(code, dean));
            Assert.True(dean.Count > head.Count);
        }

        [Fact]
        public void Administrative_permissions_are_never_part_of_a_default_role()
        {
            var reserved = new[]
            {
            Permissions.Users.Manage, Permissions.Roles.Manage, Permissions.PermissionAdmin.Manage,
            Permissions.Settings.Manage, Permissions.Audit.Read
        };

            foreach (var (role, codes) in DefaultRolePermissions.ByRole)
            {
                foreach (var code in reserved)
                {
                    Assert.DoesNotContain(code, codes);
                }
            }
        }

        [Fact]
        public void Students_and_instructors_have_no_management_or_grade_approval_rights_by_default()
        {
            foreach (var role in new[] { RoleNames.Student, RoleNames.Instructor })
            {
                var codes = DefaultRolePermissions.ByRole[role];

                Assert.DoesNotContain(codes, c => c.EndsWith(".Manage", StringComparison.Ordinal));
                Assert.DoesNotContain(Permissions.Grades.Approve, codes);
                Assert.DoesNotContain(Permissions.Grades.Publish, codes);
            }
        }

        // ---------- Permission ----------

        [Theory]
        [InlineData("Grades.Publish")]
        [InlineData("Users.Read")]
        [InlineData("Permissions.Manage")]
        public void Permission_accepts_Module_dot_Action_codes(string code)
            => Assert.True(Permission.Create(code, "Name", null, "Module").IsSuccess);

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("grades.publish")]
        [InlineData("Grades.publish")]
        [InlineData("Grades")]
        [InlineData("Grades.")]
        [InlineData("Grades.Publish.All")]
        [InlineData("Grades Publish")]
        [InlineData("1Grades.Publish")]
        public void Permission_rejects_malformed_codes(string? code)
        {
            var result = Permission.Create(code, "Name", null, "Module");

            Assert.Equal("PERMISSION_CODE_INVALID", result.Error?.Code);
        }

        [Fact]
        public void Permission_rejects_overlong_values_and_missing_data()
        {
            var longCode = "A" + new string('a', 100) + ".B";

            Assert.Equal("PERMISSION_CODE_INVALID", Permission.Create(longCode, "N", null, "M").Error?.Code);
            Assert.Equal("PERMISSION_DATA_INVALID", Permission.Create("A.B", "", null, "M").Error?.Code);
            Assert.Equal("PERMISSION_DATA_INVALID", Permission.Create("A.B", "N", null, " ").Error?.Code);
            Assert.Equal("PERMISSION_DATA_INVALID", Permission.Create("A.B", "N", new string('x', 501), "M").Error?.Code);
        }

        [Fact]
        public void Permission_trims_values_and_drops_a_blank_description()
        {
            var result = Permission.Create(" Grades.Publish ", " Publish ", "  ", " Grading ");

            Assert.True(result.TryGetValue(out var permission));
            Assert.NotNull(permission);
            Assert.Equal("Grades.Publish", permission.Code);
            Assert.Equal("Publish", permission.Name);
            Assert.Null(permission.Description);
            Assert.Equal("Grading", permission.Module);
            Assert.NotEqual(Guid.Empty, permission.Id);
        }

        // ---------- RolePermission ----------

        [Fact]
        public void RolePermission_needs_both_ids()
        {
            Assert.True(RolePermission.Create(Guid.NewGuid(), Guid.NewGuid()).IsSuccess);
            Assert.Equal("ROLE_PERMISSION_DATA_INVALID", RolePermission.Create(Guid.Empty, Guid.NewGuid()).Error?.Code);
            Assert.Equal("ROLE_PERMISSION_DATA_INVALID", RolePermission.Create(Guid.NewGuid(), Guid.Empty).Error?.Code);
        }

        // ---------- ScopeAssignment ----------

        [Theory]
        [InlineData(ScopeType.Faculty)]
        [InlineData(ScopeType.Department)]
        [InlineData(ScopeType.Section)]
        public void Local_scopes_need_a_scope_id(ScopeType type)
        {
            Assert.True(ScopeAssignment.Create(Guid.NewGuid(), Guid.NewGuid(), type, Guid.NewGuid(), Now).IsSuccess);
            Assert.Equal("SCOPE_INVALID", ScopeAssignment.Create(Guid.NewGuid(), Guid.NewGuid(), type, null, Now).Error?.Code);
            Assert.Equal("SCOPE_INVALID", ScopeAssignment.Create(Guid.NewGuid(), Guid.NewGuid(), type, Guid.Empty, Now).Error?.Code);
        }

        [Fact]
        public void Global_scope_must_not_have_a_scope_id()
        {
            var ok = ScopeAssignment.Create(Guid.NewGuid(), Guid.NewGuid(), ScopeType.Global, null, Now);
            var withId = ScopeAssignment.Create(Guid.NewGuid(), Guid.NewGuid(), ScopeType.Global, Guid.NewGuid(), Now);

            Assert.True(ok.IsSuccess);
            Assert.Null(ok.Value?.ScopeId);
            Assert.Equal("SCOPE_INVALID", withId.Error?.Code);
        }

        [Fact]
        public void Scope_assignment_rejects_empty_ids_and_unknown_scope_types()
        {
            Assert.True(ScopeAssignment.Create(Guid.Empty, Guid.NewGuid(), ScopeType.Global, null, Now).IsFailure);
            Assert.True(ScopeAssignment.Create(Guid.NewGuid(), Guid.Empty, ScopeType.Global, null, Now).IsFailure);
            Assert.True(ScopeAssignment.Create(Guid.NewGuid(), Guid.NewGuid(), (ScopeType)99, Guid.NewGuid(), Now).IsFailure);
        }

        [Fact]
        public void Scope_assignment_records_who_what_where_and_when()
        {
            var role = Guid.NewGuid();
            var user = Guid.NewGuid();
            var faculty = Guid.NewGuid();

            var result = ScopeAssignment.Create(role, user, ScopeType.Faculty, faculty, Now);

            Assert.True(result.TryGetValue(out var scope));
            Assert.NotNull(scope);
            Assert.Equal(role, scope.RoleId);
            Assert.Equal(user, scope.UserId);
            Assert.Equal(ScopeType.Faculty, scope.ScopeType);
            Assert.Equal(faculty, scope.ScopeId);
            Assert.Equal(Now, scope.CreatedAt);
        }
    }

}
