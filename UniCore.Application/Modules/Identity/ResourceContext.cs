using UniCore.Domain.Modules.Identity;

namespace UniCore.Application.Modules.Identity
{
    // Describes the resource a permission is checked against.
    // ScopeType/ScopeId: where it sits in the university hierarchy.
    // OwnerUserIds: users who own it or are assigned to it (the student of a record, the instructors of a section).
    public sealed record ResourceContext(ScopeType? ScopeType = null,Guid? ScopeId = null,IReadOnlyCollection<Guid>? OwnerUserIds = null)
    {
        public static ResourceContext InScope(ScopeType scopeType, Guid scopeId) => new(scopeType, scopeId);

        public static ResourceContext OwnedBy(params Guid[] userIds) => new(OwnerUserIds: userIds);

        public ResourceContext WithOwners(params Guid[] userIds) => this with { OwnerUserIds = userIds };
    }



}
