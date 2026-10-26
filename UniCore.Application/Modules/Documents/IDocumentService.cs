using UniCore.Domain.Common.Results;

namespace UniCore.Application.Modules.Documents
{
    // For the OTHER modules (admissions, attendance excuses, certificates). It checks the file and stores it, but it
    // does not check who is asking: the caller does. The API goes through DocumentHandler, which does.
    public interface IDocumentService
    {
        // typeCode: DocumentTypeCodes.* or any active type. ownerUserId = null for a document made by the system.
        Task<Result<DocumentDto>> UploadAsync(string? typeCode, DocumentUpload upload, Guid? ownerUserId, Guid? actorUserId, CancellationToken cancellationToken = default);

        Task<Result<DocumentDto>> AddVersionAsync(Guid documentId, DocumentUpload upload, Guid? actorUserId, CancellationToken cancellationToken = default);

        // Ok when the document exists and is not deleted. Used before another module links to a document.
        Task<Result> VerifyLinkableAsync(Guid documentId, CancellationToken cancellationToken = default);

        // versionNumber = null means the current version. The caller disposes the stream.
        Task<Result<DocumentContent>> OpenAsync(Guid documentId, int? versionNumber, CancellationToken cancellationToken = default);
    }


}
