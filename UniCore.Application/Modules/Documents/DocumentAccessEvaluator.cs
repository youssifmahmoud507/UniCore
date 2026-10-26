using UniCore.Application.Common.Abstractions;
using UniCore.Application.Common.Repositories;
using UniCore.Application.Modules.Documents.Specifications;
using UniCore.Application.Modules.Identity;
using UniCore.Domain.Modules.Documents;
using UniCore.Domain.Modules.Identity;

namespace UniCore.Application.Modules.Documents
{
    // Who can do what with a document. The first rule that applies wins:
    //  1. the owner can do everything;
    //  2. a person with a valid grant gets what the grant says;
    //  3. a SENSITIVE document stops here: nobody else, whatever their role;
    //  4. staff with Documents.Manage (university-wide) manage it, with Documents.Read they read it.
    // "No access" is reported as "not found", so a person cannot learn that a document exists.
    public sealed class DocumentAccessEvaluator(IAggregateRepository<DocumentAccess> accesses, IPermissionAuthorizer authorizer, ICurrentUser user, IClock clock)
    {
        private static readonly ResourceContext GlobalResource = ResourceContext.InScope(ScopeType.Global, Guid.Empty);

        private readonly IAggregateRepository<DocumentAccess> _accesses = accesses;
        private readonly IPermissionAuthorizer _authorizer = authorizer;
        private readonly ICurrentUser _user = user;
        private readonly IClock _clock = clock;

        public async Task<DocumentAccessLevel> EvaluateAsync(Document document, DocumentType type, CancellationToken cancellationToken = default)
        {
            if (!_user.IsAuthenticated || _user.UserId is not { } me)
            {
                return DocumentAccessLevel.None;
            }

            if (document.OwnerUserId == me)
            {
                return DocumentAccessLevel.Manage;
            }

            var grant = await _accesses.FirstOrDefaultAsync(new ActiveAccessForUserSpecification(document.Id, me), cancellationToken);

            if (grant is not null && grant.IsValidAt(_clock.UtcNow))
            {
                return grant.AccessType == DocumentAccessType.Manage ? DocumentAccessLevel.Manage : DocumentAccessLevel.Read;
            }

            if (type.IsSensitive)
            {
                return DocumentAccessLevel.None;
            }

            if ((await _authorizer.AuthorizeAsync(Permissions.Documents.Manage, GlobalResource, cancellationToken)).IsSuccess)
            {
                return DocumentAccessLevel.Manage;
            }

            if ((await _authorizer.AuthorizeAsync(Permissions.Documents.Read, GlobalResource, cancellationToken)).IsSuccess)
            {
                return DocumentAccessLevel.Read;
            }

            return DocumentAccessLevel.None;
        }
    }


}
