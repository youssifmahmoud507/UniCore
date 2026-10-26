using UniCore.Application.Common.Abstractions;
using UniCore.Application.Common.Pagination;
using UniCore.Application.Common.Repositories;
using UniCore.Application.Modules.Documents.Specifications;
using UniCore.Application.Modules.Identity;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Audit;
using UniCore.Domain.Modules.Documents;
using UniCore.Domain.Modules.Identity;

namespace UniCore.Application.Modules.Documents
{
    // What the API calls. It adds the "who is asking" part around IDocumentService: upload permission, the access
    // rules (see DocumentAccessEvaluator) and the audit trail. Every read of a SENSITIVE document is audited.
    public sealed class DocumentHandler(IPermissionAuthorizer authorizer, ICurrentUser user, IDocumentService documents, DocumentAccessEvaluator access, IAggregateRepository<Document> documentRepository, IAggregateRepository<DocumentType> types, IAggregateRepository<DocumentAccess> accesses, IUserDirectory directory, IUnitOfWork unitOfWork, IClock clock, IAuditWriter audit)
    {
        private sealed record Loaded(Document Document, DocumentType Type, DocumentAccessLevel Level);

        private readonly IPermissionAuthorizer _authorizer = authorizer;
        private readonly ICurrentUser _user = user;
        private readonly IDocumentService _documents = documents;
        private readonly DocumentAccessEvaluator _access = access;
        private readonly IAggregateRepository<Document> _documentRepository = documentRepository;
        private readonly IAggregateRepository<DocumentType> _types = types;
        private readonly IAggregateRepository<DocumentAccess> _accesses = accesses;
        private readonly IUserDirectory _directory = directory;
        private readonly IUnitOfWork _unitOfWork = unitOfWork;
        private readonly IClock _clock = clock;
        private readonly IAuditWriter _audit = audit;

        private Task AuditAsync(string action, Guid entityId, RequestInfo request, string? newValues, CancellationToken cancellationToken) => _audit.WriteAsync(
            new AuditEntry(action, _user.UserId, "Document", entityId.ToString(), request.IpAddress, request.UserAgent, null, newValues),
            cancellationToken);

        // Everybody who is signed in and holds the permission can upload their own documents.
        private Task<Result> RequireUploadAsync(CancellationToken cancellationToken)
            => !_user.IsAuthenticated || _user.UserId is not { } me
                ? Task.FromResult(Result.Fail(Error.Unauthorized()))
                : _authorizer.AuthorizeAsync(Permissions.Documents.Upload, ResourceContext.OwnedBy(me), cancellationToken);

        // A document the person cannot use is "not found", whatever the reason.
        private async Task<Result<Loaded>> LoadAsync(Guid documentId, DocumentAccessLevel needed, CancellationToken cancellationToken)
        {
            var document = await _documentRepository.FirstOrDefaultAsync(new DocumentByIdSpecification(documentId), cancellationToken);
            if (document is null || document.Status == DocumentStatus.Deleted)
            {
                return DocumentErrors.NotFound;
            }

            var type = await _types.FirstOrDefaultAsync(new DocumentTypeByIdSpecification(document.DocumentTypeId), cancellationToken);
            if (type is null)
            {
                return DocumentErrors.NotFound;
            }

            var level = await _access.EvaluateAsync(document, type, cancellationToken);

            return level >= needed ? new Loaded(document, type, level) : DocumentErrors.NotFound;
        }

        public async Task<Result<DocumentDto>> UploadAsync(string? typeCode, DocumentUpload upload, RequestInfo request, CancellationToken cancellationToken = default)
        {
            var auth = await RequireUploadAsync(cancellationToken);
            if (auth.IsFailure)
            {
                return Result<DocumentDto>.Fail(auth.Errors);
            }

            var created = await _documents.UploadAsync(typeCode, upload, _user.UserId, _user.UserId, cancellationToken);
            if (!created.TryGetValue(out var document))
            {
                return Result<DocumentDto>.Fail(created.Errors);
            }

            await AuditAsync(AuditActions.DocumentUploaded, document.Id, request, $"{document.TypeCode} {document.FileName} {document.SizeBytes} bytes", cancellationToken);

            return document;
        }

        public async Task<Result<DocumentDto>> AddVersionAsync(Guid documentId, DocumentUpload upload, RequestInfo request, CancellationToken cancellationToken = default)
        {
            var auth = await RequireUploadAsync(cancellationToken);
            if (auth.IsFailure)
            {
                return Result<DocumentDto>.Fail(auth.Errors);
            }

            var loaded = await LoadAsync(documentId, DocumentAccessLevel.Manage, cancellationToken);
            if (!loaded.TryGetValue(out var found))
            {
                return Result<DocumentDto>.Fail(loaded.Errors);
            }

            var added = await _documents.AddVersionAsync(found.Document.Id, upload, _user.UserId, cancellationToken);
            if (!added.TryGetValue(out var document))
            {
                return Result<DocumentDto>.Fail(added.Errors);
            }

            await AuditAsync(AuditActions.DocumentVersionAdded, document.Id, request, $"version {document.CurrentVersion} {document.FileName}", cancellationToken);

            return document;
        }

        public async Task<Result<DocumentDto>> GetAsync(Guid documentId, RequestInfo request, CancellationToken cancellationToken = default)
        {
            var loaded = await LoadAsync(documentId, DocumentAccessLevel.Read, cancellationToken);
            if (!loaded.TryGetValue(out var found))
            {
                return Result<DocumentDto>.Fail(loaded.Errors);
            }

            if (found.Type.IsSensitive)
            {
                await AuditAsync(AuditActions.DocumentAccessed, found.Document.Id, request, "view", cancellationToken);
            }

            return DocumentMapper.ToDto(found.Document, found.Type);
        }

        // versionNumber = null is the current version.
        public async Task<Result<DocumentContent>> DownloadAsync(Guid documentId, int? versionNumber, RequestInfo request, CancellationToken cancellationToken = default)
        {
            var loaded = await LoadAsync(documentId, DocumentAccessLevel.Read, cancellationToken);
            if (!loaded.TryGetValue(out var found))
            {
                return Result<DocumentContent>.Fail(loaded.Errors);
            }

            var content = await _documents.OpenAsync(found.Document.Id, versionNumber, cancellationToken);
            if (!content.TryGetValue(out var file))
            {
                return Result<DocumentContent>.Fail(content.Errors);
            }

            if (found.Type.IsSensitive)
            {
                await AuditAsync(AuditActions.DocumentAccessed, found.Document.Id, request, $"download version {versionNumber ?? found.Document.CurrentVersionNumber}", cancellationToken);
            }

            return file;
        }

        // The documents the signed-in person owns.
        public async Task<Result<PagedResult<DocumentDto>>> ListMineAsync(PaginationQuery page, CancellationToken cancellationToken = default)
        {
            if (!_user.IsAuthenticated || _user.UserId is not { } me)
            {
                return Error.Unauthorized();
            }

            var specification = new DocumentsOfOwnerSpecification(me, page);

            var items = await _documentRepository.ListAsync(specification, cancellationToken);
            var total = await _documentRepository.CountAsync(specification, cancellationToken);

            var typeIds = items.Select(d => d.DocumentTypeId).Distinct().ToList();
            var found = typeIds.Count == 0 ? [] : await _types.ListAsync(new DocumentTypesByIdsSpecification(typeIds), cancellationToken);
            var byId = found.ToDictionary(t => t.Id);

            var dtos = items.Where(d => byId.ContainsKey(d.DocumentTypeId)).Select(d => DocumentMapper.ToDto(d, byId[d.DocumentTypeId])).ToList();

            return PagedResult<DocumentDto>.Create(dtos, page, total);
        }

        // Soft delete: the status changes and the files stay (a document can be needed again for an audit).
        public async Task<Result<DocumentDto>> DeleteAsync(Guid documentId, RequestInfo request, CancellationToken cancellationToken = default)
        {
            var loaded = await LoadAsync(documentId, DocumentAccessLevel.Manage, cancellationToken);
            if (!loaded.TryGetValue(out var found))
            {
                return Result<DocumentDto>.Fail(loaded.Errors);
            }

            var tracked = await _documentRepository.FirstOrDefaultAsync(new DocumentByIdSpecification(found.Document.Id, tracking: true), cancellationToken);
            if (tracked is null)
            {
                return DocumentErrors.NotFound;
            }

            var deleted = tracked.MarkDeleted(_user.UserId, _clock.UtcNow);
            if (deleted.IsFailure)
            {
                return Result<DocumentDto>.Fail(deleted.Errors);
            }

            var saved = await _unitOfWork.SaveChangesAsync(cancellationToken);
            if (saved.IsFailure)
            {
                return Result<DocumentDto>.Fail(saved.Errors);
            }

            await AuditAsync(AuditActions.DocumentDeleted, tracked.Id, request, found.Type.Code, cancellationToken);

            return DocumentMapper.ToDto(tracked, found.Type);
        }

        // ---------- sharing ----------

        public async Task<Result<DocumentAccessDto>> GrantAccessAsync(Guid documentId, GrantAccessCommand command, RequestInfo request, CancellationToken cancellationToken = default)
        {
            var loaded = await LoadAsync(documentId, DocumentAccessLevel.Manage, cancellationToken);
            if (!loaded.TryGetValue(out var found))
            {
                return Result<DocumentAccessDto>.Fail(loaded.Errors);
            }

            if (command.UserId is not { } userId || userId == Guid.Empty || !await _directory.IsActiveAsync(userId, cancellationToken))
            {
                return DocumentErrors.AccessInvalid;
            }

            if (await _accesses.AnyAsync(new ActiveAccessForUserSpecification(found.Document.Id, userId), cancellationToken))
            {
                return DocumentErrors.AccessExists;
            }

            var granted = DocumentAccess.Grant(found.Document.Id, userId, command.AccessType ?? DocumentAccessType.Read, command.ExpiresAt, _user.UserId, _clock.UtcNow);
            if (!granted.TryGetValue(out var grant))
            {
                return Result<DocumentAccessDto>.Fail(granted.Errors);
            }

            _accesses.Add(grant);

            var saved = await _unitOfWork.SaveChangesAsync(cancellationToken);
            if (saved.IsFailure)
            {
                return saved.Error?.Code == CommonErrors.DuplicateResource.Code
                    ? DocumentErrors.AccessExists
                    : Result<DocumentAccessDto>.Fail(saved.Errors);
            }

            await AuditAsync(AuditActions.DocumentAccessGranted, found.Document.Id, request, $"user {userId} {grant.AccessType}", cancellationToken);

            return DocumentMapper.ToDto(grant);
        }

        public async Task<Result<DocumentAccessDto>> RevokeAccessAsync(Guid documentId, Guid accessId, RequestInfo request, CancellationToken cancellationToken = default)
        {
            var loaded = await LoadAsync(documentId, DocumentAccessLevel.Manage, cancellationToken);
            if (!loaded.TryGetValue(out var found))
            {
                return Result<DocumentAccessDto>.Fail(loaded.Errors);
            }

            var grant = await _accesses.FirstOrDefaultAsync(new DocumentAccessByIdSpecification(accessId, tracking: true), cancellationToken);
            if (grant is null || grant.DocumentId != found.Document.Id)
            {
                return DocumentErrors.AccessNotFound;
            }

            var revoked = grant.Revoke(_user.UserId, _clock.UtcNow);
            if (revoked.IsFailure)
            {
                return Result<DocumentAccessDto>.Fail(revoked.Errors);
            }

            var saved = await _unitOfWork.SaveChangesAsync(cancellationToken);
            if (saved.IsFailure)
            {
                return Result<DocumentAccessDto>.Fail(saved.Errors);
            }

            await AuditAsync(AuditActions.DocumentAccessRevoked, found.Document.Id, request, $"user {grant.UserId}", cancellationToken);

            return DocumentMapper.ToDto(grant);
        }

        public async Task<Result<IReadOnlyList<DocumentAccessDto>>> ListAccessAsync(Guid documentId, CancellationToken cancellationToken = default)
        {
            var loaded = await LoadAsync(documentId, DocumentAccessLevel.Manage, cancellationToken);
            if (!loaded.TryGetValue(out var found))
            {
                return Result<IReadOnlyList<DocumentAccessDto>>.Fail(loaded.Errors);
            }

            var items = await _accesses.ListAsync(new AccessesOfDocumentSpecification(found.Document.Id), cancellationToken);

            return Result<IReadOnlyList<DocumentAccessDto>>.Ok([.. items.Select(a => DocumentMapper.ToDto(a))]);
        }
    }


}
