using UniCore.Domain.Modules.Audit;
using UniCore.Domain.Modules.Identity;

namespace UniCore.UnitTests.Modules.Identity
{
    public class RoleManagementHandlerTests
    {
        private static RbacTestEnv Admin()
        {
            var env = new RbacTestEnv();
            env.SignInAsSuperAdmin();
            return env;
        }

        private static RbacTestEnv Limited(params string[] permissions)
        {
            var env = new RbacTestEnv();
            env.SignIn(Guid.NewGuid(), TestGrants.Role("Registrar", permissions));
            return env;
        }

        // ---------- reading ----------

        [Fact]
        public async Task Listing_permissions_needs_Roles_Manage()
        {
            var env = Limited(Permissions.Grades.Read);

            var result = await env.RoleHandler.ListPermissionsAsync();

            Assert.Equal("FORBIDDEN_PERMISSION_MISSING", result.Error?.Code);
        }

        [Fact]
        public async Task Admin_sees_the_whole_catalog()
        {
            var env = Admin();

            var result = await env.RoleHandler.ListPermissionsAsync();

            Assert.True(result.TryGetValue(out var list));
            Assert.NotNull(list);
            Assert.Equal(Permissions.All.Count, list.Count);
        }

        [Fact]
        public async Task Listing_roles_shows_counts_and_the_built_in_flag()
        {
            var env = Admin();

            var result = await env.RoleHandler.ListRolesAsync();

            Assert.True(result.TryGetValue(out var roles));
            Assert.NotNull(roles);
            Assert.Equal(RoleNames.All.Count, roles.Count);
            Assert.All(roles, r => Assert.True(r.IsBuiltIn));

            Assert.Equal(Permissions.AllCodes.Count, roles.First(r => r.Name == RoleNames.SuperAdmin).PermissionCount);
            Assert.Equal(
                DefaultRolePermissions.ByRole[RoleNames.Dean].Count,
                roles.First(r => r.Name == RoleNames.Dean).PermissionCount);
        }

        [Fact]
        public async Task Get_role_returns_sorted_permissions_and_super_admin_holds_all_of_them()
        {
            var env = Admin();

            var dean = await env.RoleHandler.GetRoleAsync(env.RoleIds[RoleNames.Dean]);
            var superAdmin = await env.RoleHandler.GetRoleAsync(env.RoleIds[RoleNames.SuperAdmin]);

            Assert.True(dean.TryGetValue(out var deanDetail));
            Assert.NotNull(deanDetail);
            Assert.Equal(deanDetail.Permissions.Order(StringComparer.Ordinal), deanDetail.Permissions);

            Assert.True(superAdmin.TryGetValue(out var adminDetail));
            Assert.NotNull(adminDetail);
            Assert.Equal(Permissions.AllCodes.Count, adminDetail.Permissions.Count);
        }

        [Fact]
        public async Task Get_unknown_role_is_not_found()
        {
            var env = Admin();

            var result = await env.RoleHandler.GetRoleAsync(Guid.NewGuid());

            Assert.Equal("ROLE_NOT_FOUND", result.Error?.Code);
        }

        // ---------- create ----------

        [Fact]
        public async Task Create_role_works_and_is_audited()
        {
            var env = Admin();

            var result = await env.RoleHandler.CreateRoleAsync("Auditor", env.Request);

            Assert.True(result.TryGetValue(out var role));
            Assert.NotNull(role);
            Assert.Equal("Auditor", role.Name);
            Assert.False(role.IsBuiltIn);

            var entry = Assert.Single(env.AuditOf(AuditActions.RoleCreated));
            Assert.Equal(env.ActorId, entry.ActorUserId);
            Assert.Equal("Auditor", entry.NewValues);
        }

        [Fact]
        public async Task Create_role_rejects_blank_and_duplicate_names()
        {
            var env = Admin();

            var blank = await env.RoleHandler.CreateRoleAsync("  ", env.Request);
            var duplicate = await env.RoleHandler.CreateRoleAsync("dean", env.Request);

            Assert.Equal("ROLE_NAME_REQUIRED", blank.Error?.Code);
            Assert.Equal("ROLE_ALREADY_EXISTS", duplicate.Error?.Code);
            Assert.Empty(env.AuditOf(AuditActions.RoleCreated));
        }

        [Fact]
        public async Task Create_role_needs_Roles_Manage()
        {
            var env = Limited(Permissions.Users.Read);

            var result = await env.RoleHandler.CreateRoleAsync("Auditor", env.Request);

            Assert.Equal("FORBIDDEN_PERMISSION_MISSING", result.Error?.Code);
            Assert.DoesNotContain("Auditor", env.Identity.Roles.Values);
        }

        // ---------- delete ----------

        [Fact]
        public async Task Built_in_roles_cannot_be_deleted()
        {
            var env = Admin();

            var result = await env.RoleHandler.DeleteRoleAsync(env.RoleIds[RoleNames.Student], env.Request);

            Assert.Equal("ROLE_PROTECTED", result.Error?.Code);
            Assert.Contains(RoleNames.Student, env.Identity.Roles.Values);
        }

        [Fact]
        public async Task A_role_that_still_has_users_cannot_be_deleted()
        {
            var env = Admin();
            var roleId = env.AddCustomRole("Auditor", Permissions.Audit.Read);
            env.Identity.SeedUser("someone", true, "Auditor");

            var result = await env.RoleHandler.DeleteRoleAsync(roleId, env.Request);

            Assert.Equal("ROLE_IN_USE", result.Error?.Code);
            Assert.Contains("Auditor", env.Identity.Roles.Values);
        }

        [Fact]
        public async Task Deleting_an_unused_custom_role_removes_its_permissions_and_is_audited()
        {
            var env = Admin();
            var roleId = env.AddCustomRole("Auditor", Permissions.Audit.Read, Permissions.Reports.Read);
            Assert.Equal(2, env.PermissionCountOf(roleId));

            var result = await env.RoleHandler.DeleteRoleAsync(roleId, env.Request);

            Assert.True(result.IsSuccess);
            Assert.DoesNotContain("Auditor", env.Identity.Roles.Values);
            Assert.Equal(0, env.PermissionCountOf(roleId));
            Assert.Equal("Auditor", Assert.Single(env.AuditOf(AuditActions.RoleDeleted)).OldValues);
        }

        [Fact]
        public async Task Deleting_an_unknown_role_is_not_found()
        {
            var env = Admin();

            var result = await env.RoleHandler.DeleteRoleAsync(Guid.NewGuid(), env.Request);

            Assert.Equal("ROLE_NOT_FOUND", result.Error?.Code);
        }

        // ---------- permissions of a role ----------

        [Fact]
        public async Task Changing_role_permissions_needs_Permissions_Manage_not_just_Roles_Manage()
        {
            var env = Limited(Permissions.Roles.Manage);

            var result = await env.RoleHandler.SetRolePermissionsAsync(
                env.RoleIds[RoleNames.Student], new[] { Permissions.Grades.Read }, env.Request);

            Assert.Equal("FORBIDDEN_PERMISSION_MISSING", result.Error?.Code);
        }

        [Fact]
        public async Task A_missing_permission_list_is_a_validation_error()
        {
            var env = Admin();

            var result = await env.RoleHandler.SetRolePermissionsAsync(env.RoleIds[RoleNames.Student], null, env.Request);

            Assert.Equal("PERMISSIONS_REQUIRED", result.Error?.Code);
        }

        [Fact]
        public async Task Super_admin_permissions_cannot_be_changed()
        {
            var env = Admin();

            var result = await env.RoleHandler.SetRolePermissionsAsync(
                env.RoleIds[RoleNames.SuperAdmin], new[] { Permissions.Grades.Read }, env.Request);

            Assert.Equal("ROLE_IMMUTABLE", result.Error?.Code);
        }

        [Fact]
        public async Task Unknown_permission_codes_are_rejected_and_nothing_changes()
        {
            var env = Admin();
            var studentId = env.RoleIds[RoleNames.Student];
            var before = env.PermissionCountOf(studentId);

            var result = await env.RoleHandler.SetRolePermissionsAsync(
                studentId, new[] { Permissions.Grades.Read, "Nothing.Here" }, env.Request);

            Assert.Equal("PERMISSION_NOT_FOUND", result.Error?.Code);
            Assert.Contains("Nothing.Here", result.Error?.Description);
            Assert.Equal(before, env.PermissionCountOf(studentId));
            Assert.Empty(env.Audit.Entries);
        }

        [Fact]
        public async Task Setting_the_same_permissions_changes_nothing_and_writes_no_audit()
        {
            var env = Admin();
            var studentId = env.RoleIds[RoleNames.Student];

            var result = await env.RoleHandler.SetRolePermissionsAsync(
                studentId, DefaultRolePermissions.ByRole[RoleNames.Student].ToArray(), env.Request);

            Assert.True(result.IsSuccess);
            Assert.Equal(0, env.Uow.SaveCalls);
            Assert.Empty(env.Audit.Entries);
        }

        [Fact]
        public async Task Adding_a_permission_grants_it_and_audits_only_the_grant()
        {
            var env = Admin();
            var studentId = env.RoleIds[RoleNames.Student];
            var wanted = DefaultRolePermissions.ByRole[RoleNames.Student].Append(Permissions.Documents.Manage).ToArray();

            var result = await env.RoleHandler.SetRolePermissionsAsync(studentId, wanted, env.Request);

            Assert.True(result.TryGetValue(out var detail));
            Assert.NotNull(detail);
            Assert.Contains(Permissions.Documents.Manage, detail.Permissions);
            Assert.Equal(wanted.Length, env.PermissionCountOf(studentId));
            Assert.Equal(Permissions.Documents.Manage, Assert.Single(env.AuditOf(AuditActions.PermissionGranted)).NewValues);
            Assert.Empty(env.AuditOf(AuditActions.PermissionRevoked));
        }

        [Fact]
        public async Task Replacing_the_set_removes_the_rest_normalises_input_and_audits_the_revocation()
        {
            var env = Admin();
            var studentId = env.RoleIds[RoleNames.Student];

            var result = await env.RoleHandler.SetRolePermissionsAsync(
                studentId,
                new[] { Permissions.Grades.Read, " Finance.Read ", Permissions.Grades.Read, "  " },
                env.Request);

            Assert.True(result.TryGetValue(out var detail));
            Assert.NotNull(detail);
            Assert.Equal(new[] { Permissions.Finance.Read, Permissions.Grades.Read }, detail.Permissions);
            Assert.Equal(2, env.PermissionCountOf(studentId));

            var revoked = Assert.Single(env.AuditOf(AuditActions.PermissionRevoked));
            Assert.NotNull(revoked.OldValues);
            Assert.Contains(Permissions.Registration.Register, revoked.OldValues);
            Assert.Empty(env.AuditOf(AuditActions.PermissionGranted));
        }

        [Fact]
        public async Task Nobody_can_hand_out_a_permission_they_do_not_hold()
        {
            var env = Limited(Permissions.PermissionAdmin.Manage, Permissions.Grades.Read);
            var instructorId = env.RoleIds[RoleNames.Instructor];
            var before = env.PermissionCountOf(instructorId);

            var wanted = DefaultRolePermissions.ByRole[RoleNames.Instructor].Append(Permissions.Refunds.Approve).ToArray();
            var result = await env.RoleHandler.SetRolePermissionsAsync(instructorId, wanted, env.Request);

            Assert.Equal("CANNOT_GRANT_PERMISSION_YOU_LACK", result.Error?.Code);
            Assert.Equal(before, env.PermissionCountOf(instructorId));
            Assert.Empty(env.Audit.Entries);
        }

        [Fact]
        public async Task A_limited_manager_can_still_remove_permissions()
        {
            var env = Limited(Permissions.PermissionAdmin.Manage);
            var instructorId = env.RoleIds[RoleNames.Instructor];

            var result = await env.RoleHandler.SetRolePermissionsAsync(instructorId, Array.Empty<string>(), env.Request);

            Assert.True(result.IsSuccess);
            Assert.Equal(0, env.PermissionCountOf(instructorId));
            Assert.Single(env.AuditOf(AuditActions.PermissionRevoked));
        }

        [Fact]
        public async Task Save_failures_are_returned_and_not_audited()
        {
            var env = Admin();
            env.Uow.FailWith = UniCore.Domain.Common.Results.CommonErrors.DatabaseUpdateFailed;

            var result = await env.RoleHandler.SetRolePermissionsAsync(
                env.RoleIds[RoleNames.Student], Array.Empty<string>(), env.Request);

            Assert.True(result.IsFailure);
            Assert.Empty(env.Audit.Entries);
        }
    }

}
