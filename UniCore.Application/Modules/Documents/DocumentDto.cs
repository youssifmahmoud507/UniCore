using UniCore.Domain.Modules.Documents;

namespace UniCore.Application.Modules.Documents
{
    public sealed record DocumentDto(Guid Id, string TypeCode, bool IsSensitive, string FileName, string ContentType, long SizeBytes, int CurrentVersion, DocumentStatus Status, Guid? OwnerUserId, DateTimeOffset CreatedAt, DateTimeOffset? UpdatedAt, IReadOnlyList<DocumentVersionDto> Versions);


}
