using System.Data.Common;
using Microsoft.Extensions.Logging;
using UniCore.Application.Common.Repositories;
using UniCore.Application.Modules.Students;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Calendar;
using UniCore.Domain.Modules.University;

namespace UniCore.Infrastructure.Modules.University
{
    // Real implementation of the partner's IAcademicPlacementVerifier (replaces UnverifiedAcademicPlacementVerifier).
    // WHAT IT CHECKS NOW: the program exists, is active, and ProgramCode (when given) matches it;
    // the academic year exists, is not closed, and AdmissionYear matches its start year;
    // the academic level (when given) exists, is active and belongs to the program.
    // WHAT IT CANNOT CHECK YET: the study plan version (StudyPlanVersion arrives in Week 6).
    public sealed class AcademicPlacementVerifier(
        IRepository<AcademicProgram> programs, IRepository<AcademicYear> years, IRepository<AcademicLevel> levels,
        ILogger<AcademicPlacementVerifier> logger) : IAcademicPlacementVerifier
    {
        private readonly IRepository<AcademicProgram> _programs = programs;
        private readonly IRepository<AcademicYear> _years = years;
        private readonly IRepository<AcademicLevel> _levels = levels;
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

            var year = await CheckYearAsync(placement.AdmissionAcademicYearId, placement.AdmissionYear, cancellationToken);
            if (year.IsFailure)
            {
                return year;
            }

            var level = await CheckLevelAsync(found.Id, placement.AcademicLevelId, cancellationToken);
            if (level.IsFailure)
            {
                return level;
            }

            // TODO Week 6: study plan version belongs to the program.
            return Result.Ok();
        }

        public async Task<Result> VerifyProgramChangeAsync(Guid academicProgramId, Guid studyPlanVersionId, Guid? academicLevelId, CancellationToken cancellationToken = default)
        {
            var program = await FindProgramAsync(academicProgramId, cancellationToken);
            if (!program.TryGetValue(out var found))
            {
                return Result.Fail(program.Errors);
            }

            var level = await CheckLevelAsync(found.Id, academicLevelId, cancellationToken);
            if (level.IsFailure)
            {
                return level;
            }

            // TODO Week 6: study plan version belongs to the program.
            return Result.Ok();
        }

        // The year exists, is not closed, and the admission year is the calendar year the academic year starts in.
        private async Task<Result> CheckYearAsync(Guid academicYearId, int admissionYear, CancellationToken cancellationToken)
        {
            try
            {
                var year = await _years.GetByIdAsync(academicYearId, cancellationToken);
                if (year is null)
                {
                    return Result.Fail(CalendarErrors.AcademicYearNotFound);
                }

                if (year.Status == AcademicYearStatus.Closed)
                {
                    return Result.Fail(CalendarErrors.AcademicYearClosed);
                }

                return year.StartDate.Year == admissionYear ? Result.Ok() : Result.Fail(CalendarErrors.AdmissionYearMismatch);
            }
            catch (OperationCanceledException)
            {
                return Result.Fail(CommonErrors.OperationCancelled);
            }
            catch (DbException)
            {
                _logger.LogError("Could not read academic year {AcademicYearId} while verifying a placement.", academicYearId);
                return Result.Fail(CommonErrors.DatabaseTimeout);
            }
        }

        // No level means "not placed in a level yet", which is allowed.
        private async Task<Result> CheckLevelAsync(Guid programId, Guid? academicLevelId, CancellationToken cancellationToken)
        {
            if (academicLevelId is not { } levelId)
            {
                return Result.Ok();
            }

            try
            {
                var level = await _levels.GetByIdAsync(levelId, cancellationToken);
                if (level is null)
                {
                    return Result.Fail(CalendarErrors.LevelNotFound);
                }

                if (level.AcademicProgramId != programId)
                {
                    return Result.Fail(CalendarErrors.LevelProgramMismatch);
                }

                return level.IsActive ? Result.Ok() : Result.Fail(CalendarErrors.LevelInactive);
            }
            catch (OperationCanceledException)
            {
                return Result.Fail(CommonErrors.OperationCancelled);
            }
            catch (DbException)
            {
                _logger.LogError("Could not read academic level {AcademicLevelId} while verifying a placement.", levelId);
                return Result.Fail(CommonErrors.DatabaseTimeout);
            }
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
