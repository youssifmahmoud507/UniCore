using UniCore.Application.Modules.Admissions;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Admissions;

namespace UniCore.Infrastructure.Modules.Admissions
{
    // Placeholder until the Documents module (Developer 2) exists: any non-empty id is accepted.
    // Replace the registration in AddAdmissionsModule with the real verifier; nothing else changes.
    public sealed class UnverifiedAdmissionDocumentVerifier : IAdmissionDocumentVerifier
    {
        public Task<Result> VerifyAsync(Guid documentId, CancellationToken cancellationToken = default)
            => Task.FromResult(documentId == Guid.Empty ? Result.Fail(AdmissionsErrors.DocumentInvalid) : Result.Ok());
    }
}
