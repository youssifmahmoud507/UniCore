using UniCore.Application.Common.Abstractions;
using UniCore.Application.Common.Repositories;
using UniCore.Application.Modules.Documents.Specifications;
using UniCore.Application.Modules.Identity;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Audit;
using UniCore.Domain.Modules.Documents;
using UniCore.Domain.Modules.Identity;

namespace UniCore.Application.Modules.Documents
{
    public sealed class DocumentTypeHandler(IPermissionAuthorizer authorizer, ICurrentUser user, IAggregateRepository<DocumentType> types, IUnitOfWork unitOfWork, IClock clock, IAuditWriter audit)
    {
        private static readonly ResourceContext GlobalResource = ResourceContext.InScope(ScopeType.Global, Guid.Empty);

        private readonly IPermissionAuthorizer _authorizer = authorizer;
        private readonly ICurrentUser _user = user;
        private readonly IAggregateRepository<DocumentType> _types = types;
        private readonly IUnitOfWork _unitOfWork = unitOfWork;
        private readonly IClock _clock = clock;
        private readonly IAuditWriter _audit = audit;

        // Anybody signed in can see which kinds of document exist: they need it to upload.
        public async Task<Result<IReadOnlyList<DocumentTypeDto>>> ListAsync(CancellationToken cancellationToken = default)
        {
            if (!_user.IsAuthenticated)
            {
                return Error.Unauthorized();
            }

            var items = await _types.ListAsync(new DocumentTypesSpecification(), cancellationToken);

            return Result<IReadOnlyList<DocumentTypeDto>>.Ok([.. items.Select(t => DocumentMapper.ToDto(t))]);
        }

        public async Task<Result<DocumentTypeDto>> CreateAsync(CreateDocumentTypeCommand command, RequestInfo request, CancellationToken cancellationToken = default)
        {
            var auth = await _authorizer.AuthorizeAsync(Permissions.Documents.Manage, GlobalResource, cancellationToken);
            if (auth.IsFailure)
            {
                return Result<DocumentTypeDto>.Fail(auth.Errors);
            }

            if (command.AllowedFormats is not { Count: > 0 } formats)
            {
                return DocumentErrors.TypeDataInvalid;
            }

            var allowed = formats.Aggregate(DocumentFormat.None, (all, format) => all | format);

            var code = DocumentType.NormalizeCode(command.Code);
            if (code is not null && await _types.AnyAsync(new DocumentTypeByCodeSpecification(code), cancellationToken))
            {
                return DocumentErrors.TypeCodeExists;
            }

            var created = DocumentType.Create(command.Code, command.Name, command.IsSensitive, allowed, _clock.UtcNow);
            if (!created.TryGetValue(out var type))
            {
                return Result<DocumentTypeDto>.Fail(created.Errors);
            }

            _types.Add(type);

            var saved = await _unitOfWork.SaveChangesAsync(cancellationToken);
            if (saved.IsFailure)
            {
                return saved.Error?.Code == CommonErrors.DuplicateResource.Code
                    ? DocumentErrors.TypeCodeExists
                    : Result<DocumentTypeDto>.Fail(saved.Errors);
            }

            await _audit.WriteAsync(
                new AuditEntry(AuditActions.DocumentTypeCreated, _user.UserId, "DocumentType", type.Id.ToString(), request.IpAddress, request.UserAgent, null, $"{type.Code} sensitive {type.IsSensitive}"),
                cancellationToken);

            return DocumentMapper.ToDto(type);
        }
    }


}
