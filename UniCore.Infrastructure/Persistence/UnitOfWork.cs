using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using UniCore.Application.Common.Abstractions;
using UniCore.Domain.Common.Results;

namespace UniCore.Infrastructure.Persistence
{
    public sealed class UnitOfWork(AppDbContext db, ILogger<UnitOfWork> logger) : IUnitOfWork
    {
        private const int UniqueIndexViolation = 2601;
        private const int UniqueConstraintViolation = 2627;
        private const int SqlTimeout = -2;
        private readonly AppDbContext _db = db;
        private readonly ILogger<UnitOfWork> _logger = logger;
        public async Task<Result<int>> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var rows = await _db.SaveChangesAsync(cancellationToken);
                return Result<int>.Ok(rows);
            }
            // DbUpdateConcurrencyException derives from DbUpdateException, so it must come first.
            catch (DbUpdateConcurrencyException)
            {
                _logger.LogWarning("Concurrency conflict while saving changes.");
                return Result<int>.Fail(CommonErrors.ConcurrencyConflict);
            }
            catch (DbUpdateException ex) when (SqlErrorNumber(ex) is UniqueIndexViolation or UniqueConstraintViolation)
            {
                // Not logging the exception: SQL messages for unique violations contain the duplicate value.
                _logger.LogWarning("Unique constraint violation while saving changes (SQL error {SqlError}).", SqlErrorNumber(ex));
                return Result<int>.Fail(CommonErrors.DuplicateResource);
            }
            catch (DbUpdateException ex) when (SqlErrorNumber(ex) == SqlTimeout)
            {
                _logger.LogError("Database timeout while saving changes.");
                return Result<int>.Fail(CommonErrors.DatabaseTimeout);
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError("Database update failed ({ExceptionType}, SQL error {SqlError}).",
                    ex.GetType().Name, SqlErrorNumber(ex));
                return Result<int>.Fail(CommonErrors.DatabaseUpdateFailed);
            }
            catch (TimeoutException)
            {
                _logger.LogError("Timeout while saving changes.");
                return Result<int>.Fail(CommonErrors.DatabaseTimeout);
            }
            catch (OperationCanceledException)
            {
                return Result<int>.Fail(CommonErrors.OperationCancelled);
            }
        }

        private static int? SqlErrorNumber(DbUpdateException ex) => (ex.InnerException as SqlException)?.Number;
    }
}
