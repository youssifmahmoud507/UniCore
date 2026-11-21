using UniCore.Domain.Common.Results;

namespace UniCore.Application.Modules.Calendar
{
    // Other modules veto closing a semester (for example Grading while grades are unpublished). Every registered guard runs
    // when a semester is moved to Closed; the first failure stops it. No permission check: the caller already holds University.Manage.
    public interface ISemesterCloseGuard
    {
        Task<Result> CanCloseAsync(Guid semesterId, CancellationToken cancellationToken = default);
    }
}
