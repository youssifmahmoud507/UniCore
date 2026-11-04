using UniCore.Application.Common.Abstractions;
using UniCore.Application.Common.Pagination;
using UniCore.Application.Common.Repositories;
using UniCore.Application.Modules.Notifications;
using UniCore.Application.Modules.Workflow.Specifications;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Workflow;

namespace UniCore.Application.Modules.Workflow
{
    // A request nobody has touched for a long time is cancelled, and its requester is told. This stops requests
    // from sitting forever in somebody's inbox. "Touched" means any action: the UpdatedAt of the instance moves
    // on every approval, return or resubmission.
    public sealed class ExpiredRequestsService(IAggregateRepository<WorkflowInstance> instances, INotificationPublisher notifications, IUnitOfWork unitOfWork, IClock clock)
    {
        public const string ExpiryComment = "Expired: no action was taken on this request for too long.";

        private readonly IAggregateRepository<WorkflowInstance> _instances = instances;
        private readonly INotificationPublisher _notifications = notifications;
        private readonly IUnitOfWork _unitOfWork = unitOfWork;
        private readonly IClock _clock = clock;

        // Read-only: the ids of the open requests with no activity for this many days.
        public async Task<Result<IReadOnlyList<Guid>>> FindStaleAsync(int inactiveDays, CancellationToken cancellationToken = default)
        {
            var cutoff = _clock.UtcNow.AddDays(-inactiveDays);
            var ids = new List<Guid>();

            for (var page = 1; ; page++)
            {
                var batch = await _instances.ListAsync(new StaleInstancesSpecification(cutoff, new PaginationQuery(page, PaginationQuery.MaxPageSize)), cancellationToken);

                ids.AddRange(batch.Select(instance => instance.Id));

                if (batch.Count < PaginationQuery.MaxPageSize)
                {
                    break;
                }
            }

            return Result<IReadOnlyList<Guid>>.Ok(ids);
        }

        // Ok(true): cancelled now. Ok(false): nothing to do (it moved on or was already closed since it was found).
        // The requester cancels, so the usual "somebody else cancelled your request" notification is not sent;
        // the requester gets their own message here, once (the instance id is the event id).
        public async Task<Result<bool>> ExpireAsync(Guid instanceId, int inactiveDays, CancellationToken cancellationToken = default)
        {
            var cutoff = _clock.UtcNow.AddDays(-inactiveDays);

            var instance = await _instances.FirstOrDefaultAsync(new StaleInstanceByIdSpecification(instanceId, cutoff), cancellationToken);
            if (instance is null)
            {
                return false;
            }

            var cancelled = instance.Cancel(instance.RequesterUserId, false, ExpiryComment, _clock.UtcNow);
            if (cancelled.IsFailure)
            {
                return Result<bool>.Fail(cancelled.Errors);
            }

            var saved = await _unitOfWork.SaveChangesAsync(cancellationToken);
            if (saved.IsFailure)
            {
                // Somebody acted on it at the same moment (a concurrency conflict): leave it, it is not stale any more.
                return saved.Error?.Code == CommonErrors.ConcurrencyConflict.Code ? false : Result<bool>.Fail(saved.Errors);
            }

            await _notifications.PublishAsync(new NotificationRequest(instance.RequesterUserId, NotificationTypes.RequestStatusChanged, "Your request has expired", $"Your {instance.DefinitionCode.Replace('_', ' ').ToLowerInvariant()} was cancelled because nobody acted on it for {inactiveDays} days. You can submit it again.", "WorkflowInstance", instance.Id, instance.Id),cancellationToken);

            return true;
        }
    }


}
