using UniCore.Domain.Common.Entities;

namespace UniCore.Domain.Modules.Documents
{
    public sealed class DocumentVersion : BaseEntity
    {
        private DocumentVersion()
        {
        }

        public Guid DocumentId { get; private set; }
        public int VersionNumber { get; private set; }
        public string FileName { get; private set; } = string.Empty;
        public string ContentType { get; private set; } = string.Empty;
        public long SizeBytes { get; private set; }
        public string StorageKey { get; private set; } = string.Empty;
        public string Checksum { get; private set; } = string.Empty;
        public Guid? CreatedByUserId { get; private set; }
        public DateTimeOffset CreatedAt { get; private set; }

        internal static DocumentVersion Create(Guid documentId, int versionNumber, DocumentVersionData data, DateTimeOffset now) => new()
        {
            Id = Guid.NewGuid(),
            DocumentId = documentId,
            VersionNumber = versionNumber,
            FileName = data.FileName,
            ContentType = data.ContentType,
            SizeBytes = data.SizeBytes,
            StorageKey = data.StorageKey,
            Checksum = data.Checksum,
            CreatedByUserId = data.CreatedByUserId,
            CreatedAt = now
        };
    }



}
