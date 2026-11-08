using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using UniCore.Application.Common.Abstractions;
using UniCore.Domain.Modules.Documents;
using UniCore.Domain.Modules.Identity;
using UniCore.Infrastructure.Modules.Identity;

namespace UniCore.Infrastructure.Modules.Caching
{
    // Which saved changes make which cache group stale. One place, so a new way of changing roles or document types
    // (in any module) is covered without remembering to clear the cache.
    public static class CacheInvalidationRules
    {
        public static string? GroupFor(EntityEntry entry)
        {
            if (entry.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted))
            {
                return null;
            }

            return entry.Entity switch
            {
                RolePermission or ScopeAssignment or Permission or IdentityUserRole<Guid> or ApplicationRole => CacheGroups.UserAccess,
                ApplicationUser => UserAccessChanged(entry) ? CacheGroups.UserAccess : null,
                DocumentType => CacheGroups.DocumentTypes,
                _ => null
            };
        }

        // A new user has no cached access yet, and logging in only touches other columns. Only the active flag (or
        // removing the user) changes what the user may do.
        private static bool UserAccessChanged(EntityEntry entry)
            => entry.State == EntityState.Deleted
                || (entry.State == EntityState.Modified && entry.Property(nameof(ApplicationUser.IsActive)).IsModified);
    }



}
