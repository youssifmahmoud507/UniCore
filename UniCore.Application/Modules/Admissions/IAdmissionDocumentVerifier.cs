using UniCore.Domain.Common.Results;

namespace UniCore.Application.Modules.Admissions
{
    // Contract with the Documents module (Developer 2): is this document real, and may it be linked to an application?
    // Admissions stores only the document id and never the file. Until Documents exists, the Infrastructure
    // default accepts any non-empty id (Unverified..., like the placement verifier was).
    public interface IAdmissionDocumentVerifier
    {
        Task<Result> VerifyAsync(Guid documentId, CancellationToken cancellationToken = default);
    }
}
