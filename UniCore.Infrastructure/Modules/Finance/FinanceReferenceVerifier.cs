using Microsoft.EntityFrameworkCore;
using System.Data.Common;
using UniCore.Application.Modules.Finance;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Finance;
using UniCore.Domain.Modules.University;
using UniCore.Infrastructure.Persistence;

namespace UniCore.Infrastructure.Modules.Finance
{
    // Checks the ids against Developer 1's University module: the university must exist; the program must
    // exist and be active. The semester is accepted as it is until his academic calendar is merged.
    public sealed class FinanceReferenceVerifier(AppDbContext db) : IFinanceReferenceVerifier
    {
        private readonly AppDbContext _db = db;

        public async Task<Result> VerifyUniversityAsync(Guid universityId, CancellationToken cancellationToken = default)
        {
            try
            {
                var exists = await _db.Set<Domain.Modules.University.University>().AsNoTracking().AnyAsync(u => u.Id == universityId, cancellationToken);

                return exists ? Result.Ok() : Result.Fail(UniversityErrors.UniversityNotFound);
            }
            catch (OperationCanceledException)
            {
                return Result.Fail(CommonErrors.OperationCancelled);
            }
            catch (Exception ex) when (ex is DbException or TimeoutException or InvalidOperationException)
            {
                return Result.Fail(FinanceErrors.ReferenceCheckUnavailable);
            }
        }

        public async Task<Result> VerifyProgramAsync(Guid academicProgramId, CancellationToken cancellationToken = default)
        {
            try
            {
                var program = await _db.Set<AcademicProgram>().AsNoTracking().Where(p => p.Id == academicProgramId).Select(p => new { p.IsActive }).FirstOrDefaultAsync(cancellationToken);

                if (program is null)
                {
                    return Result.Fail(UniversityErrors.ProgramNotFound);
                }

                return program.IsActive ? Result.Ok() : Result.Fail(UniversityErrors.ProgramInactive);
            }
            catch (OperationCanceledException)
            {
                return Result.Fail(CommonErrors.OperationCancelled);
            }
            catch (Exception ex) when (ex is DbException or TimeoutException or InvalidOperationException)
            {
                return Result.Fail(FinanceErrors.ReferenceCheckUnavailable);
            }
        }

        // TODO (Developer 1, Week 4): check the semester against his AcademicSemester.
        public Task<Result> VerifySemesterAsync(Guid academicSemesterId, CancellationToken cancellationToken = default)
            => Task.FromResult(Result.Ok());
    }

}
