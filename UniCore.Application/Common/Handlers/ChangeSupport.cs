using UniCore.Application.Common.Abstractions;
using UniCore.Domain.Common.Results;

namespace UniCore.Application.Common.Handlers
{
    // The last step of every change handler: save, and only when the save worked, write the audit entry.
    public static class ChangeSupport
    {
        public static async Task<Result> CommitAsync(IUnitOfWork unitOfWork, IAuditWriter audit, AuditEntry entry, CancellationToken cancellationToken)
        {
            var saved = await unitOfWork.SaveChangesAsync(cancellationToken);
            if (saved.IsFailure)
            {
                return Result.Fail(saved.Errors);
            }

            await audit.WriteAsync(entry, cancellationToken);
            return Result.Ok();
        }
    }
}
