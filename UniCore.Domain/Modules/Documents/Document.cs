using UniCore.Domain.Common.Entities;
using UniCore.Domain.Common.Results;

namespace UniCore.Domain.Modules.Documents
{
    // A document with all its versions. Old versions are never changed or removed. Deleting is a status change:
    // the files stay on disk (a document can be needed again for an audit).
    public sealed class Document : AggregateRoot
    {
        public const int MaxFileNameLength = 150;
        public const int MaxContentTypeLength = 100;
        public const int StorageKeyLength = 32;
        public const int ChecksumLength = 64;

        private readonly List<DocumentVersion> _versions = [];

        private Document()
        {
        }

        public Guid DocumentTypeId { get; private set; }

        // Null for a document made by the system (a generated transcript).
        public Guid? OwnerUserId { get; private set; }

        // These three describe the CURRENT version.
        public string FileName { get; private set; } = string.Empty;
        public string ContentType { get; private set; } = string.Empty;
        public long SizeBytes { get; private set; }

        public int CurrentVersionNumber { get; private set; }
        public DocumentStatus Status { get; private set; }
        public DateTimeOffset CreatedAt { get; private set; }
        public DateTimeOffset? UpdatedAt { get; private set; }
        public DateTimeOffset? DeletedAt { get; private set; }
        public Guid? DeletedByUserId { get; private set; }

        public IReadOnlyList<DocumentVersion> Versions => _versions.AsReadOnly();

        // EF returns children in any order, so the current version is found by its number.
        public DocumentVersion? Current => _versions.FirstOrDefault(v => v.VersionNumber == CurrentVersionNumber);

        public DocumentVersion? Version(int versionNumber) => _versions.FirstOrDefault(v => v.VersionNumber == versionNumber);

        public static Result<Document> Create(Guid documentTypeId, Guid? ownerUserId, DocumentVersionData data, DateTimeOffset now)
        {
            if (documentTypeId == Guid.Empty || !IsValid(data))
            {
                return DocumentErrors.DataInvalid;
            }

            var document = new Document
            {
                Id = Guid.NewGuid(),
                DocumentTypeId = documentTypeId,
                OwnerUserId = ownerUserId,
                Status = DocumentStatus.Active,
                CreatedAt = now
            };

            document.Apply(data, 1, now);

            return document;
        }

        public Result<DocumentVersion> AddVersion(DocumentVersionData data, DateTimeOffset now)
        {
            if (Status == DocumentStatus.Deleted)
            {
                return DocumentErrors.Deleted;
            }

            if (!IsValid(data))
            {
                return DocumentErrors.DataInvalid;
            }

            return Apply(data, CurrentVersionNumber + 1, now);
        }

        public Result MarkDeleted(Guid? deletedByUserId, DateTimeOffset now)
        {
            if (Status == DocumentStatus.Deleted)
            {
                return Result.Fail(DocumentErrors.Deleted);
            }

            Status = DocumentStatus.Deleted;
            DeletedAt = now;
            DeletedByUserId = deletedByUserId;
            UpdatedAt = now;

            return Result.Ok();
        }

        private DocumentVersion Apply(DocumentVersionData data, int versionNumber, DateTimeOffset now)
        {
            var version = DocumentVersion.Create(Id, versionNumber, data, now);
            _versions.Add(version);

            CurrentVersionNumber = versionNumber;
            FileName = data.FileName;
            ContentType = data.ContentType;
            SizeBytes = data.SizeBytes;
            UpdatedAt = versionNumber == 1 ? null : now;

            return version;
        }

        private static bool IsValid(DocumentVersionData data) => !string.IsNullOrWhiteSpace(data.FileName) && data.FileName.Length <= MaxFileNameLength && !string.IsNullOrWhiteSpace(data.ContentType) && data.ContentType.Length <= MaxContentTypeLength && data.SizeBytes > 0 && IsHex(data.StorageKey, StorageKeyLength) && IsHex(data.Checksum, ChecksumLength);
        private static bool IsHex(string? value, int length) => value is not null && value.Length == length && value.All(c => c is (>= '0' and <= '9') or (>= 'a' and <= 'f'));
    }



}
