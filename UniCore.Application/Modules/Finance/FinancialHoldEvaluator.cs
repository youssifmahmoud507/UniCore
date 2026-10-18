using UniCore.Application.Common.Abstractions;
using UniCore.Application.Common.Pagination;
using UniCore.Application.Common.Repositories;
using UniCore.Application.Modules.Finance.Specifications;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Audit;
using UniCore.Domain.Modules.Finance;

namespace UniCore.Application.Modules.Finance
{
    // Decides, for one student, whether an automatic hold must exist: place it, release it, or leave things as they are.
    // It has no permission check on purpose: it runs from the daily job and from outbox events, not from a request.
    public sealed class FinancialHoldEvaluator(IAggregateRepository<Invoice> invoices, IAggregateRepository<FinancialHold> holds, IFinancialHoldPolicy policy, IUnitOfWork unitOfWork, IClock clock, IAuditWriter audit)
    {
        private static readonly HoldEvaluationResult NoChange = new(0, 0, 0);

        private readonly IAggregateRepository<Invoice> _invoices = invoices;
        private readonly IAggregateRepository<FinancialHold> _holds = holds;
        private readonly IFinancialHoldPolicy _policy = policy;
        private readonly IUnitOfWork _unitOfWork = unitOfWork;
        private readonly IClock _clock = clock;
        private readonly IAuditWriter _audit = audit;

        // today is a parameter so the same code can be checked "as of" another date.
        public async Task<Result<HoldEvaluationResult>> EvaluateStudentAsync(Guid studentId, DateOnly today, CancellationToken cancellationToken = default)
        {
            var cutoff = FinancialHoldRules.OverdueCutoff(today, _policy.GraceDays);
            var overdueInvoices = await _invoices.ListAsync(new OverdueInvoicesForStudentSpecification(studentId, cutoff), cancellationToken);

            var overdue = Money.Zero;

            foreach (var invoice in overdueInvoices)
            {
                var sum = overdue.Add(invoice.Balance);
                if (!sum.TryGetValue(out var next))
                {
                    return Result<HoldEvaluationResult>.Fail(sum.Errors);
                }

                overdue = next;
            }

            return await ApplyAsync(studentId, overdue, cancellationToken);
        }

        // Read-only. Finds the students that need a look: every student with an overdue total, and every student
        // that has an automatic hold (so it can be released when they no longer owe).
        public async Task<Result<IReadOnlyList<HoldCandidate>>> FindCandidatesAsync(DateOnly today, CancellationToken cancellationToken = default)
        {
            var cutoff = FinancialHoldRules.OverdueCutoff(today, _policy.GraceDays);
            var overdueByStudent = new Dictionary<Guid, Money>();

            for (var page = 1; ; page++)
            {
                var batch = await _invoices.ListAsync(new OverdueInvoicesSpecification(cutoff, new PaginationQuery(page, PaginationQuery.MaxPageSize)), cancellationToken);

                foreach (var invoice in batch)
                {
                    var sum = overdueByStudent.GetValueOrDefault(invoice.StudentId).Add(invoice.Balance);
                    if (!sum.TryGetValue(out var next))
                    {
                        return Result<IReadOnlyList<HoldCandidate>>.Fail(sum.Errors);
                    }

                    overdueByStudent[invoice.StudentId] = next;
                }

                if (batch.Count < PaginationQuery.MaxPageSize)
                {
                    break;
                }
            }

            var withHold = new HashSet<Guid>();

            for (var page = 1; ; page++)
            {
                var batch = await _holds.ListAsync(new ActiveAutomaticHoldsSpecification(new PaginationQuery(page, PaginationQuery.MaxPageSize)), cancellationToken);

                foreach (var hold in batch)
                {
                    withHold.Add(hold.StudentId);
                }

                if (batch.Count < PaginationQuery.MaxPageSize)
                {
                    break;
                }
            }

            var candidates = overdueByStudent.Keys.Union(withHold)
                .Select(studentId => new HoldCandidate(studentId, overdueByStudent.GetValueOrDefault(studentId)))
                .ToList();

            return Result<IReadOnlyList<HoldCandidate>>.Ok(candidates);
        }

        public async Task<Result<HoldEvaluationResult>> ApplyAsync(Guid studentId, Money overdue, CancellationToken cancellationToken = default)
        {
            var minimum = Money.Rounded(_policy.MinimumOverdueBalance);
            if (!minimum.TryGetValue(out var minimumAmount))
            {
                return Result<HoldEvaluationResult>.Fail(minimum.Errors);
            }

            var shouldHold = FinancialHoldRules.ShouldHold(overdue, minimumAmount);
            var active = await _holds.FirstOrDefaultAsync(new ActiveAutomaticHoldSpecification(studentId, tracking: true), cancellationToken);
            var now = _clock.UtcNow;

            if (shouldHold && active is null)
            {
                return await PlaceAsync(studentId, overdue, now, cancellationToken);
            }

            if (!shouldHold && active is not null)
            {
                return await ReleaseAsync(active, now, cancellationToken);
            }

            return NoChange;
        }

        private async Task<Result<HoldEvaluationResult>> PlaceAsync(Guid studentId, Money overdue, DateTimeOffset now, CancellationToken cancellationToken)
        {
            // A person released an automatic hold recently: leave the student alone for a while.
            if (_policy.WaiverDays > 0
                && await _holds.AnyAsync(new RecentWaiverSpecification(studentId, now.AddDays(-_policy.WaiverDays)), cancellationToken))
            {
                return NoChange;
            }

            var created = FinancialHold.PlaceAutomatic(studentId, overdue, now);
            if (!created.TryGetValue(out var hold))
            {
                return Result<HoldEvaluationResult>.Fail(created.Errors);
            }

            _holds.Add(hold);

            var saved = await _unitOfWork.SaveChangesAsync(cancellationToken);
            if (saved.IsFailure)
            {
                // A parallel evaluation placed it first: nothing more to do.
                return saved.Error?.Code == CommonErrors.DuplicateResource.Code
                    ? NoChange
                    : Result<HoldEvaluationResult>.Fail(saved.Errors);
            }

            await _audit.WriteAsync(
                new AuditEntry(AuditActions.FinancialHoldPlaced, null, "FinancialHold", hold.Id.ToString(), NewValues: $"student {studentId} overdue {overdue}"),
                cancellationToken);

            return new HoldEvaluationResult(1, 0, 0);
        }

        private async Task<Result<HoldEvaluationResult>> ReleaseAsync(FinancialHold hold, DateTimeOffset now, CancellationToken cancellationToken)
        {
            var released = hold.Release(null, null, now);
            if (released.IsFailure)
            {
                return Result<HoldEvaluationResult>.Fail(released.Errors);
            }

            var saved = await _unitOfWork.SaveChangesAsync(cancellationToken);
            if (saved.IsFailure)
            {
                // Somebody else released it at the same time: fine.
                return saved.Error?.Code == CommonErrors.ConcurrencyConflict.Code ? NoChange : Result<HoldEvaluationResult>.Fail(saved.Errors);
            }

            await _audit.WriteAsync(new AuditEntry(AuditActions.FinancialHoldReleased, null, "FinancialHold", hold.Id.ToString(), OldValues: FinancialHoldStatus.Active.ToString(), NewValues: "Overdue balance cleared"), cancellationToken);

            return new HoldEvaluationResult(0, 1, 0);
        }
    }




}
