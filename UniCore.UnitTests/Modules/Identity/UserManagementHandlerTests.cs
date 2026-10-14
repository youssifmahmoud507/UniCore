using UniCore.Application.Common.Pagination;
using UniCore.Domain.Modules.Audit;
using UniCore.Domain.Modules.Identity;

namespace UniCore.UnitTests.Modules.Identity
{
    public class UserManagementHandlerTests
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
            env.SignIn(env.ActorId, TestGrants.Role("Registrar", permissions));
            return env;
        }

        private static readonly Guid FacultyA = Guid.NewGuid();
        private static readonly Guid FacultyB = Guid.NewGuid();

        // ---------- create / read ----------

        [Fact]
        public async Task Create_user_stores_the_account_queues_the_welcome_email_and_audits()
        {
            var env = Admin();

            var result = await env.UserHandler.CreateUserAsync("dean1", "dean1@uni.edu", null, env.Request);

            Assert.True(result.TryGetValue(out var user));
            Assert.NotNull(user);
            Assert.True(user.IsActive);

            var email = Assert.Single(env.Emails.Messages);
            Assert.Equal("dean1@uni.edu", email.To);
            Assert.Contains("account was created", email.Subject);
            Assert.Contains("Forgot password", email.TextBody);
            // The mail explains how to set a password; it must never contain one.
            Assert.DoesNotContain("temporary password", email.TextBody, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("your password is", email.TextBody, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("temporary password", email.HtmlBody, StringComparison.OrdinalIgnoreCase);
            var entry = Assert.Single(env.AuditOf(AuditActions.UserCreated));
            Assert.Equal(env.ActorId, entry.ActorUserId);
            Assert.Equal(user.Id.ToString(), entry.EntityId);
        }

        [Fact]
        public async Task Create_user_needs_Users_Manage()
        {
            var env = Limited(Permissions.Users.Read);

            var result = await env.UserHandler.CreateUserAsync("dean1", "dean1@uni.edu", null, env.Request);

            Assert.Equal("FORBIDDEN_PERMISSION_MISSING", result.Error?.Code);
            Assert.Empty(env.Emails.Messages);
            Assert.Empty(env.Identity.Users);
        }

        [Fact]
        public async Task Create_user_rejects_bad_input_and_duplicates_without_sending_mail()
        {
            var env = Admin();
            env.Identity.SeedUser("taken");

            var badEmail = await env.UserHandler.CreateUserAsync("x", "not-an-email", null, env.Request);
            var noName = await env.UserHandler.CreateUserAsync(" ", "x@uni.edu", null, env.Request);
            var duplicate = await env.UserHandler.CreateUserAsync("other", "taken@uni.edu", null, env.Request);

            Assert.Equal("USER_EMAIL_INVALID", badEmail.Error?.Code);
            Assert.Equal("USER_NAME_REQUIRED", noName.Error?.Code);
            Assert.Equal("USER_ALREADY_EXISTS", duplicate.Error?.Code);
            Assert.Empty(env.Emails.Messages);
            Assert.Empty(env.AuditOf(AuditActions.UserCreated));
        }

        [Fact]
        public async Task A_full_email_queue_does_not_fail_the_creation_but_is_audited()
        {
            var env = Admin();
            env.Emails.Full = true;

            var result = await env.UserHandler.CreateUserAsync("dean1", "dean1@uni.edu", null, env.Request);

            Assert.True(result.IsSuccess);
            Assert.Single(env.AuditOf(AuditActions.EmailEnqueueFailed));
        }

        [Fact]
        public async Task Listing_and_reading_users_needs_Users_Read()
        {
            var env = Limited(Permissions.Grades.Read);

            var list = await env.UserHandler.ListUsersAsync(new PaginationQuery(), null, null);
            var one = await env.UserHandler.GetUserAsync(Guid.NewGuid());

            Assert.Equal("FORBIDDEN_PERMISSION_MISSING", list.Error?.Code);
            Assert.Equal("FORBIDDEN_PERMISSION_MISSING", one.Error?.Code);
        }

        [Fact]
        public async Task Listing_pages_searches_and_filters_by_status()
        {
            var env = Admin(); // "root" is already a user
            env.Identity.SeedUser("alice");
            env.Identity.SeedUser("bob");
            env.Identity.SeedUser("carol", false);
            env.Identity.SeedUser("dean1");

            var firstPage = await env.UserHandler.ListUsersAsync(new PaginationQuery(1, 2), null, null);
            var search = await env.UserHandler.ListUsersAsync(new PaginationQuery(), "  DEAN ", null);
            var inactive = await env.UserHandler.ListUsersAsync(new PaginationQuery(), null, false);

            Assert.True(firstPage.TryGetValue(out var page));
            Assert.NotNull(page);
            Assert.Equal(2, page.Items.Count);
            Assert.Equal(5, page.TotalCount);
            Assert.True(page.HasNext);

            Assert.True(search.TryGetValue(out var found));
            Assert.NotNull(found);
            Assert.Equal("dean1", Assert.Single(found.Items).UserName);

            Assert.True(inactive.TryGetValue(out var off));
            Assert.NotNull(off);
            Assert.Equal("carol", Assert.Single(off.Items).UserName);
        }

        [Fact]
        public async Task Reading_an_unknown_user_is_not_found()
        {
            var env = Admin();

            var result = await env.UserHandler.GetUserAsync(Guid.NewGuid());

            Assert.Equal("USER_NOT_FOUND", result.Error?.Code);
        }

        // ---------- activate / deactivate ----------

        [Fact]
        public async Task Nobody_can_change_their_own_status()
        {
            var env = Admin();

            var result = await env.UserHandler.SetActiveAsync(env.ActorId, false, env.Request);

            Assert.Equal("CANNOT_MODIFY_OWN_ACCESS", result.Error?.Code);
            Assert.True(env.Identity.Find(env.ActorId)?.IsActive);
        }

        [Fact]
        public async Task Changing_the_status_of_an_unknown_user_is_not_found()
        {
            var env = Admin();

            var result = await env.UserHandler.SetActiveAsync(Guid.NewGuid(), false, env.Request);

            Assert.Equal("USER_NOT_FOUND", result.Error?.Code);
        }

        [Fact]
        public async Task Deactivating_a_user_revokes_all_their_sessions_and_nobody_elses()
        {
            var env = Admin();
            var target = env.Identity.SeedUser("dean1");
            var other = env.Identity.SeedUser("someone");
            var a = env.AddRefreshToken(target);
            var b = env.AddRefreshToken(target);
            var untouched = env.AddRefreshToken(other);

            var result = await env.UserHandler.SetActiveAsync(target, false, env.Request);

            Assert.True(result.TryGetValue(out var user));
            Assert.NotNull(user);
            Assert.False(user.IsActive);
            Assert.False(env.Identity.Find(target)?.IsActive);
            Assert.True(a.IsRevoked);
            Assert.True(b.IsRevoked);
            Assert.False(untouched.IsRevoked);
            Assert.Single(env.AuditOf(AuditActions.UserDeactivated));
        }

        [Fact]
        public async Task Setting_the_status_a_user_already_has_changes_nothing()
        {
            var env = Admin();
            var target = env.Identity.SeedUser("dean1", false);
            var token = env.AddRefreshToken(target);

            var result = await env.UserHandler.SetActiveAsync(target, false, env.Request);

            Assert.True(result.IsSuccess);
            Assert.Equal(0, env.Uow.SaveCalls);
            Assert.Empty(env.Audit.Entries);
            Assert.False(token.IsRevoked);
        }

        [Fact]
        public async Task Activating_a_user_is_audited()
        {
            var env = Admin();
            var target = env.Identity.SeedUser("dean1", false);

            var result = await env.UserHandler.SetActiveAsync(target, true, env.Request);

            Assert.True(result.IsSuccess);
            Assert.True(env.Identity.Find(target)?.IsActive);
            Assert.Single(env.AuditOf(AuditActions.UserActivated));
        }

        [Fact]
        public async Task Only_a_super_admin_can_deactivate_a_super_admin()
        {
            var env = Limited(Permissions.Users.Manage);
            var target = env.Identity.SeedUser("boss", true, RoleNames.SuperAdmin);

            var result = await env.UserHandler.SetActiveAsync(target, false, env.Request);

            Assert.Equal("ONLY_SUPERADMIN_CAN_MANAGE_SUPERADMIN", result.Error?.Code);
            Assert.True(env.Identity.Find(target)?.IsActive);
        }

        [Fact]
        public async Task A_super_admin_can_deactivate_another_one_while_a_third_stays_active()
        {
            var env = Admin(); // the actor is an active SuperAdmin too
            var target = env.Identity.SeedUser("boss", true, RoleNames.SuperAdmin);

            var result = await env.UserHandler.SetActiveAsync(target, false, env.Request);

            Assert.True(result.IsSuccess);
            Assert.False(env.Identity.Find(target)?.IsActive);
        }

        [Fact]
        public async Task The_last_active_super_admin_cannot_be_deactivated()
        {
            // Defence in depth: the actor's own record is not stored, so nobody else is active.
            var env = new RbacTestEnv();
            env.SignInAsSuperAdmin(registerActorAsUser: false);
            var target = env.Identity.SeedUser("boss", true, RoleNames.SuperAdmin);

            var result = await env.UserHandler.SetActiveAsync(target, false, env.Request);

            Assert.Equal("LAST_SUPERADMIN", result.Error?.Code);
            Assert.True(env.Identity.Find(target)?.IsActive);
        }

        // ---------- assigning roles ----------

        [Fact]
        public async Task Nobody_can_change_their_own_roles()
        {
            var env = Admin();

            var result = await env.UserHandler.AssignRoleAsync(env.ActorId, RoleNames.Student, null, null, env.Request);

            Assert.Equal("CANNOT_MODIFY_OWN_ACCESS", result.Error?.Code);
        }

        [Fact]
        public async Task Assigning_needs_a_known_role_and_an_existing_user()
        {
            var env = Admin();
            var target = env.Identity.SeedUser("dean1");

            var blank = await env.UserHandler.AssignRoleAsync(target, " ", null, null, env.Request);
            var unknownRole = await env.UserHandler.AssignRoleAsync(target, "Nope", null, null, env.Request);
            var unknownUser = await env.UserHandler.AssignRoleAsync(Guid.NewGuid(), RoleNames.Student, null, null, env.Request);

            Assert.Equal("ROLE_NAME_REQUIRED", blank.Error?.Code);
            Assert.Equal("ROLE_NOT_FOUND", unknownRole.Error?.Code);
            Assert.Equal("USER_NOT_FOUND", unknownUser.Error?.Code);
        }

        [Fact]
        public async Task Assigning_needs_Roles_Manage()
        {
            var env = Limited(Permissions.Users.Manage);
            var target = env.Identity.SeedUser("dean1");

            var result = await env.UserHandler.AssignRoleAsync(target, RoleNames.Student, null, null, env.Request);

            Assert.Equal("FORBIDDEN_PERMISSION_MISSING", result.Error?.Code);
        }

        [Fact]
        public async Task Admin_assigns_a_dean_with_a_faculty_scope_and_both_changes_are_audited()
        {
            var env = Admin();
            var target = env.Identity.SeedUser("dean1");

            var result = await env.UserHandler.AssignRoleAsync(
                target, RoleNames.Dean, ScopeType.Faculty, FacultyA, env.Request);

            Assert.True(result.TryGetValue(out var detail));
            Assert.NotNull(detail);
            Assert.Contains(RoleNames.Dean, detail.User.Roles);

            var scope = Assert.Single(detail.Scopes);
            Assert.Equal(RoleNames.Dean, scope.Role);
            Assert.Equal(ScopeType.Faculty, scope.ScopeType);
            Assert.Equal(FacultyA, scope.ScopeId);

            Assert.Single(env.ScopeRows.Items);
            Assert.Single(env.AuditOf(AuditActions.RoleAssigned));
            Assert.Single(env.AuditOf(AuditActions.ScopeAssigned));
        }

        [Fact]
        public async Task Assigning_the_same_role_and_scope_again_changes_nothing()
        {
            var env = Admin();
            var target = env.Identity.SeedUser("dean1");
            await env.UserHandler.AssignRoleAsync(target, RoleNames.Dean, ScopeType.Faculty, FacultyA, env.Request);
            var saves = env.Uow.SaveCalls;
            var audits = env.Audit.Entries.Count;

            var again = await env.UserHandler.AssignRoleAsync(target, RoleNames.Dean, ScopeType.Faculty, FacultyA, env.Request);

            Assert.True(again.IsSuccess);
            Assert.Single(env.ScopeRows.Items);
            Assert.Equal(saves, env.Uow.SaveCalls);
            Assert.Equal(audits, env.Audit.Entries.Count);
        }

        [Fact]
        public async Task A_second_scope_for_the_same_role_adds_only_the_scope()
        {
            var env = Admin();
            var target = env.Identity.SeedUser("dean1");
            await env.UserHandler.AssignRoleAsync(target, RoleNames.Dean, ScopeType.Faculty, FacultyA, env.Request);

            var result = await env.UserHandler.AssignRoleAsync(target, RoleNames.Dean, ScopeType.Faculty, FacultyB, env.Request);

            Assert.True(result.IsSuccess);
            Assert.Equal(2, env.ScopeRows.Items.Count);
            Assert.Single(env.AuditOf(AuditActions.RoleAssigned));
            Assert.Equal(2, env.AuditOf(AuditActions.ScopeAssigned).Count());
        }

        [Fact]
        public async Task An_invalid_scope_is_rejected_before_the_role_is_added()
        {
            var env = Admin();
            var target = env.Identity.SeedUser("dean1");

            var result = await env.UserHandler.AssignRoleAsync(target, RoleNames.Dean, ScopeType.Faculty, null, env.Request);

            Assert.Equal("SCOPE_INVALID", result.Error?.Code);
            Assert.False(env.Identity.IsInRole(target, RoleNames.Dean));
            Assert.Empty(env.ScopeRows.Items);
        }

        [Fact]
        public async Task A_role_without_a_scope_is_allowed()
        {
            var env = Admin();
            var target = env.Identity.SeedUser("student1");

            var result = await env.UserHandler.AssignRoleAsync(target, RoleNames.Student, null, null, env.Request);

            Assert.True(result.IsSuccess);
            Assert.True(env.Identity.IsInRole(target, RoleNames.Student));
            Assert.Empty(env.ScopeRows.Items);
        }

        [Fact]
        public async Task Only_a_super_admin_can_assign_the_super_admin_role()
        {
            var env = Limited(Permissions.Roles.Manage);
            var target = env.Identity.SeedUser("someone");

            var result = await env.UserHandler.AssignRoleAsync(target, RoleNames.SuperAdmin, null, null, env.Request);

            Assert.Equal("ONLY_SUPERADMIN_CAN_MANAGE_SUPERADMIN", result.Error?.Code);
            Assert.False(env.Identity.IsInRole(target, RoleNames.SuperAdmin));
        }

        [Fact]
        public async Task Super_admin_is_assigned_without_a_scope_and_a_scope_on_it_is_rejected()
        {
            var env = Admin();
            var target = env.Identity.SeedUser("someone");

            var withScope = await env.UserHandler.AssignRoleAsync(
                target, RoleNames.SuperAdmin, ScopeType.Faculty, FacultyA, env.Request);
            var plain = await env.UserHandler.AssignRoleAsync(target, RoleNames.SuperAdmin, null, null, env.Request);

            Assert.Equal("SCOPE_INVALID", withScope.Error?.Code);
            Assert.True(plain.IsSuccess);
            Assert.True(env.Identity.IsInRole(target, RoleNames.SuperAdmin));
        }

        [Fact]
        public async Task Only_a_super_admin_can_grant_a_global_scope()
        {
            var env = Limited(Permissions.Roles.Manage);
            var target = env.Identity.SeedUser("someone");

            var result = await env.UserHandler.AssignRoleAsync(
                target, RoleNames.Registrar, ScopeType.Global, null, env.Request);

            Assert.Equal("ONLY_SUPERADMIN_CAN_GRANT_GLOBAL_SCOPE", result.Error?.Code);
            Assert.Empty(env.ScopeRows.Items);
        }

        [Fact]
        public async Task A_super_admin_can_grant_a_global_scope()
        {
            var env = Admin();
            var target = env.Identity.SeedUser("registrar1");

            var result = await env.UserHandler.AssignRoleAsync(
                target, RoleNames.Registrar, ScopeType.Global, null, env.Request);

            Assert.True(result.IsSuccess);
            var scope = Assert.Single(env.ScopeRows.Items);
            Assert.Equal(ScopeType.Global, scope.ScopeType);
            Assert.Null(scope.ScopeId);
        }

        [Fact]
        public async Task Nobody_can_hand_out_a_role_whose_permissions_they_do_not_all_hold()
        {
            var env = Limited(Permissions.Roles.Manage, Permissions.Grades.Read);
            var target = env.Identity.SeedUser("dean1");

            var result = await env.UserHandler.AssignRoleAsync(target, RoleNames.Dean, ScopeType.Faculty, FacultyA, env.Request);

            Assert.Equal("CANNOT_GRANT_PERMISSION_YOU_LACK", result.Error?.Code);
            Assert.False(env.Identity.IsInRole(target, RoleNames.Dean));
            Assert.Empty(env.ScopeRows.Items);
        }

        [Fact]
        public async Task A_delegated_manager_can_hand_out_a_role_they_fully_hold()
        {
            var permissions = DefaultRolePermissions.ByRole[RoleNames.Dean].Append(Permissions.Roles.Manage).ToArray();
            var env = Limited(permissions);
            var target = env.Identity.SeedUser("dean2");

            var result = await env.UserHandler.AssignRoleAsync(target, RoleNames.Dean, ScopeType.Faculty, FacultyA, env.Request);

            Assert.True(result.IsSuccess);
            Assert.True(env.Identity.IsInRole(target, RoleNames.Dean));
        }

        // ---------- removing roles and scopes ----------

        [Fact]
        public async Task Removing_a_role_removes_its_scopes_and_audits_each_change()
        {
            var env = Admin();
            var target = env.Identity.SeedUser("dean1");
            await env.UserHandler.AssignRoleAsync(target, RoleNames.Dean, ScopeType.Faculty, FacultyA, env.Request);
            await env.UserHandler.AssignRoleAsync(target, RoleNames.Dean, ScopeType.Faculty, FacultyB, env.Request);

            var result = await env.UserHandler.RemoveRoleAsync(target, RoleNames.Dean, env.Request);

            Assert.True(result.TryGetValue(out var detail));
            Assert.NotNull(detail);
            Assert.DoesNotContain(RoleNames.Dean, detail.User.Roles);
            Assert.Empty(detail.Scopes);
            Assert.Empty(env.ScopeRows.Items);
            Assert.Single(env.AuditOf(AuditActions.RoleRemoved));
            Assert.Equal(2, env.AuditOf(AuditActions.ScopeRemoved).Count());
        }

        [Fact]
        public async Task Removing_a_role_the_user_does_not_hold_changes_nothing()
        {
            var env = Admin();
            var target = env.Identity.SeedUser("dean1");

            var result = await env.UserHandler.RemoveRoleAsync(target, RoleNames.Dean, env.Request);

            Assert.True(result.IsSuccess);
            Assert.Equal(0, env.Uow.SaveCalls);
            Assert.Empty(env.Audit.Entries);
        }

        [Fact]
        public async Task Nobody_can_remove_their_own_roles()
        {
            var env = Admin();

            var result = await env.UserHandler.RemoveRoleAsync(env.ActorId, RoleNames.SuperAdmin, env.Request);

            Assert.Equal("CANNOT_MODIFY_OWN_ACCESS", result.Error?.Code);
            Assert.True(env.Identity.IsInRole(env.ActorId, RoleNames.SuperAdmin));
        }

        [Fact]
        public async Task Only_a_super_admin_can_remove_the_super_admin_role()
        {
            var env = Limited(Permissions.Roles.Manage);
            var target = env.Identity.SeedUser("boss", true, RoleNames.SuperAdmin);

            var result = await env.UserHandler.RemoveRoleAsync(target, RoleNames.SuperAdmin, env.Request);

            Assert.Equal("ONLY_SUPERADMIN_CAN_MANAGE_SUPERADMIN", result.Error?.Code);
            Assert.True(env.Identity.IsInRole(target, RoleNames.SuperAdmin));
        }

        [Fact]
        public async Task The_last_active_super_admin_role_cannot_be_removed()
        {
            var env = new RbacTestEnv();
            env.SignInAsSuperAdmin(registerActorAsUser: false);
            var target = env.Identity.SeedUser("boss", true, RoleNames.SuperAdmin);

            var result = await env.UserHandler.RemoveRoleAsync(target, RoleNames.SuperAdmin, env.Request);

            Assert.Equal("LAST_SUPERADMIN", result.Error?.Code);
            Assert.True(env.Identity.IsInRole(target, RoleNames.SuperAdmin));
        }

        [Fact]
        public async Task Removing_the_super_admin_role_works_while_another_active_one_remains()
        {
            var env = Admin();
            var target = env.Identity.SeedUser("boss", true, RoleNames.SuperAdmin);

            var result = await env.UserHandler.RemoveRoleAsync(target, RoleNames.SuperAdmin, env.Request);

            Assert.True(result.IsSuccess);
            Assert.False(env.Identity.IsInRole(target, RoleNames.SuperAdmin));
        }

        [Fact]
        public async Task Removing_a_blank_role_name_is_a_validation_error()
        {
            var env = Admin();
            var target = env.Identity.SeedUser("dean1");

            var result = await env.UserHandler.RemoveRoleAsync(target, " ", env.Request);

            Assert.Equal("ROLE_NAME_REQUIRED", result.Error?.Code);
        }

        [Fact]
        public async Task Removing_one_scope_keeps_the_role_and_the_other_scopes()
        {
            var env = Admin();
            var target = env.Identity.SeedUser("dean1");
            await env.UserHandler.AssignRoleAsync(target, RoleNames.Dean, ScopeType.Faculty, FacultyA, env.Request);
            await env.UserHandler.AssignRoleAsync(target, RoleNames.Dean, ScopeType.Faculty, FacultyB, env.Request);
            var scopeToRemove = env.ScopeRows.Items.First(s => s.ScopeId == FacultyA);

            var result = await env.UserHandler.RemoveScopeAsync(target, scopeToRemove.Id, env.Request);

            Assert.True(result.TryGetValue(out var detail));
            Assert.NotNull(detail);
            Assert.Contains(RoleNames.Dean, detail.User.Roles);
            Assert.Equal(FacultyB, Assert.Single(detail.Scopes).ScopeId);
            Assert.Single(env.AuditOf(AuditActions.ScopeRemoved));
        }

        [Fact]
        public async Task A_scope_can_only_be_removed_through_its_own_user()
        {
            var env = Admin();
            var owner = env.Identity.SeedUser("dean1");
            var someoneElse = env.Identity.SeedUser("dean2");
            await env.UserHandler.AssignRoleAsync(owner, RoleNames.Dean, ScopeType.Faculty, FacultyA, env.Request);
            var scope = Assert.Single(env.ScopeRows.Items);

            var wrongUser = await env.UserHandler.RemoveScopeAsync(someoneElse, scope.Id, env.Request);
            var unknown = await env.UserHandler.RemoveScopeAsync(owner, Guid.NewGuid(), env.Request);
            var self = await env.UserHandler.RemoveScopeAsync(env.ActorId, scope.Id, env.Request);

            Assert.Equal("SCOPE_ASSIGNMENT_NOT_FOUND", wrongUser.Error?.Code);
            Assert.Equal("SCOPE_ASSIGNMENT_NOT_FOUND", unknown.Error?.Code);
            Assert.Equal("CANNOT_MODIFY_OWN_ACCESS", self.Error?.Code);
            Assert.Single(env.ScopeRows.Items);
        }
    }

}
