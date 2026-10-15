using System.Data.Common;
using Microsoft.Extensions.Logging;
using UniCore.Application.Common.Repositories;
using UniCore.Application.Modules.Students;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.University;

namespace UniCore.Infrastructure.Modules.University
{
    // Real implementation of the partner's IAcademicPlacementVerifier (replaces UnverifiedAcademicPlacementVerifier).
    // WHAT IT CHECKS NOW: the program exists, is active, and ProgramCode (when given) matches it.
    // WHAT IT CANNOT CHECK YET: the study plan version, academic level and academic year. Those entities
    // arrive in Week 4 (calendar, levels) and Week 6 (study plans); each check is added in that week.
    public sealed class AcademicPlacementVerifier(IRepository<AcademicProgram> programs, ILogger<AcademicPlacementVerifier> logger) : IAcademicPlacementVerifier
    {
        private readonly IRepository<AcademicProgram> _programs = programs;
        private readonly ILogger<AcademicPlacementVerifier> _logger = logger;

        public async Task<Result> VerifyAdmissionAsync(AdmissionPlacement placement, CancellationToken cancellationToken = default)
        {
            var program = await FindProgramAsync(placement.AcademicProgramId, cancellationToken);
            if (!program.TryGetValue(out var found))
            {
                return Result.Fail(program.Errors);
            }

            if (!string.IsNullOrWhiteSpace(placement.ProgramCode)
                && !string.Equals(placement.ProgramCode.Trim(), found.Code, StringComparison.OrdinalIgnoreCase))
            {
                return Result.Fail(UniversityErrors.ProgramCodeMismatch);
            }

            // TODO Week 4: academic year exists and AdmissionYear matches it; level belongs to the program.
            // TODO Week 6: study plan version belongs to the program.
            return Result.Ok();
        }

        public async Task<Result> VerifyProgramChangeAsync(Guid academicProgramId, Guid studyPlanVersionId, Guid? academicLevelId, CancellationToken cancellationToken = default)
        {
            var program = await FindProgramAsync(academicProgramId, cancellationToken);
            if (!program.TryGetValue(out _))
            {
                return Result.Fail(program.Errors);
            }

            // TODO Week 4 / Week 6: study plan version and level belong to the program.
            return Result.Ok();
        }

        // Exists and active, or a failed Result. A database failure is reported, never treated as "not found".
        private async Task<Result<AcademicProgram>> FindProgramAsync(Guid programId, CancellationToken cancellationToken)
        {
            try
            {
                var program = await _programs.GetByIdAsync(programId, cancellationToken);

                if (program is null)
                {
                    return UniversityErrors.ProgramNotFound;
                }

                if (!program.IsActive)
                {
                    return UniversityErrors.ProgramInactive;
                }

                return program;
            }
            catch (OperationCanceledException)
            {
                return Result<AcademicProgram>.Fail(CommonErrors.OperationCancelled);
            }
            catch (DbException)
            {
                _logger.LogError("Could not read program {ProgramId} while verifying a placement.", programId);
                return Result<AcademicProgram>.Fail(CommonErrors.DatabaseTimeout);
            }
        }
    }
}
