using UniCore.Application.Common.Abstractions;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Finance;

namespace UniCore.Application.Modules.Finance
{
    // A payment can clear what a student owes overdue, so the hold may have to be released.
    // Evaluating is idempotent, so the outbox delivering the event twice does no harm.
    public sealed class PaymentRecordedHoldHandler(FinancialHoldEvaluator evaluator, IClock clock) : IDomainEventHandler<PaymentRecorded>
    {
        private readonly FinancialHoldEvaluator _evaluator = evaluator;
        private readonly IClock _clock = clock;

        public async Task<Result> HandleAsync(PaymentRecorded domainEvent, CancellationToken cancellationToken = default)
        {
            var result = await _evaluator.EvaluateStudentAsync(domainEvent.StudentId, _clock.UtcToday, cancellationToken);

            return result.IsSuccess ? Result.Ok() : Result.Fail(result.Errors);
        }
    }




}
