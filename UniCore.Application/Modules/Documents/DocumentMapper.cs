using UniCore.Domain.Modules.Documents;

namespace UniCore.Application.Modules.Documents
{
    public static class DocumentMapper
    {
        public static DocumentVersionDto ToDto(DocumentVersion v) => new(v.VersionNumber, v.FileName, v.ContentType, v.SizeBytes, v.Checksum, v.CreatedByUserId, v.CreatedAt);

        public static DocumentDto ToDto(Document d, DocumentType type) => new(
            d.Id, type.Code, type.IsSensitive, d.FileName, d.ContentType, d.SizeBytes, d.CurrentVersionNumber, d.Status, d.OwnerUserId, d.CreatedAt, d.UpdatedAt,
            [.. d.Versions.OrderBy(v => v.VersionNumber).Select(v => ToDto(v))]);

        public static DocumentTypeDto ToDto(DocumentType t) => new(t.Id, t.Code, t.Name, t.IsSensitive, DocumentFormats.Split(t.AllowedFormats), t.IsActive);

        public static DocumentAccessDto ToDto(DocumentAccess a) => new(a.Id, a.DocumentId, a.UserId, a.AccessType, a.Status, a.GrantedAt, a.GrantedByUserId, a.ExpiresAt, a.RevokedAt);
    }


}
