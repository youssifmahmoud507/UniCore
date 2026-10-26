using System.Security.Cryptography;
using UniCore.Application.Common.Abstractions;
using UniCore.Application.Common.Repositories;
using UniCore.Application.Modules.Documents.Specifications;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Documents;

namespace UniCore.Application.Modules.Documents
{
    public sealed class DocumentService(IAggregateRepository<Document> documents, IAggregateRepository<DocumentType> types, IFileStorage storage, IFileScanner scanner, IDocumentPolicy policy, IUnitOfWork unitOfWork, IClock clock) : IDocumentService
    {
        private sealed record Prepared(byte[] Bytes, string FileName, DocumentFormat Format, string Checksum);

        private readonly IAggregateRepository<Document> _documents = documents;
        private readonly IAggregateRepository<DocumentType> _types = types;
        private readonly IFileStorage _storage = storage;
        private readonly IFileScanner _scanner = scanner;
        private readonly IDocumentPolicy _policy = policy;
        private readonly IUnitOfWork _unitOfWork = unitOfWork;
        private readonly IClock _clock = clock;

        public async Task<Result<DocumentDto>> UploadAsync(string? typeCode, DocumentUpload upload, Guid? ownerUserId, Guid? actorUserId, CancellationToken cancellationToken = default)
        {
            var code = DocumentType.NormalizeCode(typeCode);
            if (code is null)
            {
                return DocumentErrors.TypeNotFound;
            }

            var type = await _types.FirstOrDefaultAsync(new DocumentTypeByCodeSpecification(code), cancellationToken);
            if (type is null)
            {
                return DocumentErrors.TypeNotFound;
            }

            if (!type.IsActive)
            {
                return DocumentErrors.TypeInactive;
            }

            var prepared = await PrepareAsync(type, upload, cancellationToken);
            if (!prepared.TryGetValue(out var file))
            {
                return Result<DocumentDto>.Fail(prepared.Errors);
            }

            var key = Guid.NewGuid().ToString("N");

            var stored = await StoreAsync(key, file.Bytes, cancellationToken);
            if (stored.IsFailure)
            {
                return Result<DocumentDto>.Fail(stored.Errors);
            }

            var data = new DocumentVersionData(file.FileName, DocumentFormats.ContentTypeOf(file.Format), file.Bytes.Length, key, file.Checksum, actorUserId);
            var created = Document.Create(type.Id, ownerUserId, data, _clock.UtcNow);

            if (!created.TryGetValue(out var document))
            {
                await _storage.DeleteAsync(key, CancellationToken.None);
                return Result<DocumentDto>.Fail(created.Errors);
            }

            _documents.Add(document);

            var saved = await _unitOfWork.SaveChangesAsync(cancellationToken);
            if (saved.IsFailure)
            {
                // The row was not saved, so the file would be an orphan.
                await _storage.DeleteAsync(key, CancellationToken.None);
                return Result<DocumentDto>.Fail(saved.Errors);
            }

            return DocumentMapper.ToDto(document, type);
        }

        public async Task<Result<DocumentDto>> AddVersionAsync(Guid documentId, DocumentUpload upload, Guid? actorUserId, CancellationToken cancellationToken = default)
        {
            var document = await _documents.FirstOrDefaultAsync(new DocumentByIdSpecification(documentId, tracking: true), cancellationToken);
            if (document is null)
            {
                return DocumentErrors.NotFound;
            }

            if (document.Status == DocumentStatus.Deleted)
            {
                return DocumentErrors.Deleted;
            }

            var type = await _types.FirstOrDefaultAsync(new DocumentTypeByIdSpecification(document.DocumentTypeId), cancellationToken);
            if (type is null)
            {
                return DocumentErrors.TypeNotFound;
            }

            var prepared = await PrepareAsync(type, upload, cancellationToken);
            if (!prepared.TryGetValue(out var file))
            {
                return Result<DocumentDto>.Fail(prepared.Errors);
            }

            var key = Guid.NewGuid().ToString("N");

            var stored = await StoreAsync(key, file.Bytes, cancellationToken);
            if (stored.IsFailure)
            {
                return Result<DocumentDto>.Fail(stored.Errors);
            }

            var data = new DocumentVersionData(file.FileName, DocumentFormats.ContentTypeOf(file.Format), file.Bytes.Length, key, file.Checksum, actorUserId);
            var added = document.AddVersion(data, _clock.UtcNow);

            if (added.IsFailure)
            {
                await _storage.DeleteAsync(key, CancellationToken.None);
                return Result<DocumentDto>.Fail(added.Errors);
            }

            var saved = await _unitOfWork.SaveChangesAsync(cancellationToken);
            if (saved.IsFailure)
            {
                await _storage.DeleteAsync(key, CancellationToken.None);
                return Result<DocumentDto>.Fail(saved.Errors);
            }

            return DocumentMapper.ToDto(document, type);
        }

        public async Task<Result> VerifyLinkableAsync(Guid documentId, CancellationToken cancellationToken = default)
        {
            var document = await _documents.FirstOrDefaultAsync(new DocumentByIdSpecification(documentId), cancellationToken);

            if (document is null || document.Status == DocumentStatus.Deleted)
            {
                return Result.Fail(DocumentErrors.NotFound);
            }

            return Result.Ok();
        }

        public async Task<Result<DocumentContent>> OpenAsync(Guid documentId, int? versionNumber, CancellationToken cancellationToken = default)
        {
            var document = await _documents.FirstOrDefaultAsync(new DocumentByIdSpecification(documentId), cancellationToken);

            // A deleted document looks like one that never existed.
            if (document is null || document.Status == DocumentStatus.Deleted)
            {
                return DocumentErrors.NotFound;
            }

            var version = versionNumber is { } number ? document.Version(number) : document.Current;
            if (version is null)
            {
                return DocumentErrors.VersionNotFound;
            }

            var opened = await _storage.OpenReadAsync(version.StorageKey, cancellationToken);
            if (!opened.TryGetValue(out var stream))
            {
                return Result<DocumentContent>.Fail(opened.Errors);
            }

            return new DocumentContent(stream, version.FileName, version.ContentType, version.SizeBytes);
        }

        // The file is checked in this order, and the first problem stops it:
        // name and extension, extension allowed for the type, size, not empty, the real format (first bytes) equal to
        // the extension, the declared content type, then the scanner. Nothing is stored before all of them pass.
        private async Task<Result<Prepared>> PrepareAsync(DocumentType type, DocumentUpload upload, CancellationToken cancellationToken)
        {
            var fileName = DocumentFileName.Sanitize(upload.FileName);
            if (fileName is null)
            {
                return DocumentErrors.DataInvalid;
            }

            var byName = DocumentFormats.FromExtension(Path.GetExtension(fileName));
            if (byName == DocumentFormat.None || !type.Allows(byName))
            {
                return DocumentErrors.FormatNotAllowed;
            }

            var read = await ReadLimitedAsync(upload.Content, cancellationToken);
            if (!read.TryGetValue(out var bytes))
            {
                return Result<Prepared>.Fail(read.Errors);
            }

            if (bytes.Length == 0)
            {
                return DocumentErrors.FileEmpty;
            }

            var detected = DocumentFormats.Detect(bytes.AsSpan(0, Math.Min(bytes.Length, DocumentFormats.HeaderLength)));
            if (detected != byName)
            {
                return DocumentErrors.ContentMismatch;
            }

            var claimed = upload.ContentType?.Split(';')[0].Trim();
            if (!string.IsNullOrEmpty(claimed)
                && !claimed.Equals("application/octet-stream", StringComparison.OrdinalIgnoreCase)
                && !claimed.Equals(DocumentFormats.ContentTypeOf(detected), StringComparison.OrdinalIgnoreCase))
            {
                return DocumentErrors.ContentMismatch;
            }

            var scanned = await _scanner.ScanAsync(bytes, fileName, cancellationToken);
            if (scanned.IsFailure)
            {
                return Result<Prepared>.Fail(scanned.Errors);
            }

            var checksum = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

            return new Prepared(bytes, fileName, detected, checksum);
        }

        // Reads at most the allowed size. A bigger upload is refused as soon as it passes the limit.
        private async Task<Result<byte[]>> ReadLimitedAsync(Stream content, CancellationToken cancellationToken)
        {
            var limit = _policy.MaxFileSizeBytes;

            using var buffer = new MemoryStream();
            var chunk = new byte[81920];

            try
            {
                int read;

                while ((read = await content.ReadAsync(chunk, cancellationToken)) > 0)
                {
                    if (buffer.Length + read > limit)
                    {
                        return Result<byte[]>.Fail(DocumentErrors.FileTooLarge);
                    }

                    buffer.Write(chunk, 0, read);
                }
            }
            catch (OperationCanceledException)
            {
                return Result<byte[]>.Fail(CommonErrors.OperationCancelled);
            }
            catch (Exception ex) when (ex is IOException or InvalidOperationException or InvalidDataException)
            {
                return Result<byte[]>.Fail(DocumentErrors.DataInvalid);
            }

            return buffer.ToArray();
        }

        private async Task<Result> StoreAsync(string key, byte[] bytes, CancellationToken cancellationToken)
        {
            using var stream = new MemoryStream(bytes, writable: false);

            return await _storage.SaveAsync(key, stream, cancellationToken);
        }
    }


}
