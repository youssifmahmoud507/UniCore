using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using UniCore.Application.Modules.University;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Students;
using UniCore.Infrastructure.Persistence;

namespace UniCore.Infrastructure.Modules.University
{
    // Answers one question for the University module: "does any student still study in this program?".
    // It reads the Students module's table here, at the infrastructure boundary, so the University
    // application code never touches Student types.
    public sealed class ProgramUsageChecker(AppDbContext db, ILogger<ProgramUsageChecker> logger) : IProgramUsageChecker
    {
        private readonly AppDbContext _db = db;
        private readonly ILogger<ProgramUsageChecker> _logger = logger;

        public async Task<Result<bool>> HasActiveStudentsAsync(Guid academicProgramId, CancellationToken cancellationToken = default)
        {
            try
            {
                // An open history row (EndDate is null) means the student is still in the program.
                var inUse = await _db.Set<StudentProgramHistory>()
                    .AsNoTracking()
                    .AnyAsync(h => h.AcademicProgramId == academicProgramId && h.EndDate == null, cancellationToken);

                return inUse;
            }
            catch (OperationCanceledException)
            {
                return Result<bool>.Fail(CommonErrors.OperationCancelled);
            }
            catch (DbException)
            {
                _logger.LogError("Could not check whether program {ProgramId} is in use.", academicProgramId);
                return Result<bool>.Fail(CommonErrors.DatabaseTimeout);
            }
        }
    }
}
