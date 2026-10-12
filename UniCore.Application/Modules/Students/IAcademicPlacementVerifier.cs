using UniCore.Domain.Common.Results;

namespace UniCore.Application.Modules.Students
{
    // Contract implemented by Developer 1 (programs, study plans, levels, academic years).
    // It should check that every id exists, the study plan version and level belong to the program,
    // the program is active, and that ProgramCode / AdmissionYear match the real records.
    public interface IAcademicPlacementVerifier
    {
        Task<Result> VerifyAdmissionAsync(AdmissionPlacement placement, CancellationToken cancellationToken = default);

        Task<Result> VerifyProgramChangeAsync(Guid academicProgramId, Guid studyPlanVersionId, Guid? academicLevelId, CancellationToken cancellationToken = default);
    }


}
