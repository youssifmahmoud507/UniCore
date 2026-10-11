using UniCore.Domain.Common.Results;

namespace UniCore.Domain.Modules.Identity
{
    // "This user holds this role within this scope" (for example a Dean at a faculty).
    public sealed class ScopeAssignment
    {
        private ScopeAssignment()
        {
        }

        public Guid Id { get; private set; }
        public Guid RoleId { get; private set; }

        // Nullable in the data model; always set for now. A null user is reserved for future role-wide scopes.
        public Guid? UserId { get; private set; }

        public ScopeType ScopeType { get; private set; }

        // Null for Global.
        public Guid? ScopeId { get; private set; }

        public DateTimeOffset CreatedAt { get; private set; }

        public static Result<ScopeAssignment> Create(
            Guid roleId, Guid userId, ScopeType scopeType, Guid? scopeId, DateTimeOffset now)
        {
            if (roleId == Guid.Empty || userId == Guid.Empty || !Enum.IsDefined(scopeType))
            {
                return IdentityErrors.ScopeInvalid;
            }

            var globalWithId = scopeType == ScopeType.Global && scopeId is not null;
            var localWithoutId = scopeType != ScopeType.Global && (scopeId is null || scopeId == Guid.Empty);

            if (globalWithId || localWithoutId)
            {
                return IdentityErrors.ScopeInvalid;
            }

            return new ScopeAssignment
            {
                Id = Guid.NewGuid(),
                RoleId = roleId,
                UserId = userId,
                ScopeType = scopeType,
                ScopeId = scopeId,
                CreatedAt = now
            };
        }
    }
}
