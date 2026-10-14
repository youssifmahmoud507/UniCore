using UniCore.Domain.Common.Results;

namespace UniCore.Application.Modules.Finance
{
    // Checks that ids owned by Developer 1 exist. Until his modules are merged the Infrastructure
    // registers an implementation that accepts everything (UnverifiedFinanceReferenceVerifier).
    public interface IFinanceReferenceVerifier
    {
        Task<Result> VerifyUniversityAsync(Guid universityId, CancellationToken cancellationToken = default);

        Task<Result> VerifyProgramAsync(Guid academicProgramId, CancellationToken cancellationToken = default);

        Task<Result> VerifySemesterAsync(Guid academicSemesterId, CancellationToken cancellationToken = default);
    }

}
