using UniCore.Application.Modules.Students;
using UniCore.Domain.Common.Results;

namespace UniCore.Infrastructure.Modules.Students
{
    // TEMPORARY: accepts any placement. Programs, study plans, levels and academic years belong to
    // Developer 1, who replaces this class with real lookups (see IAcademicPlacementVerifier).
    public sealed class UnverifiedAcademicPlacementVerifier : IAcademicPlacementVerifier
    {
        public Task<Result> VerifyAdmissionAsync(AdmissionPlacement placement, CancellationToken cancellationToken = default)
            => Task.FromResult(Result.Ok());

        public Task<Result> VerifyProgramChangeAsync(
            Guid academicProgramId, Guid studyPlanVersionId, Guid? academicLevelId,
            CancellationToken cancellationToken = default)
            => Task.FromResult(Result.Ok());
    }
}
