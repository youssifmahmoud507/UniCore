using UniCore.Application.Common.Abstractions;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Finance;

namespace UniCore.Application.Modules.Finance
{
    // A refund puts money back on the invoice, which can make the student overdue again.
    public sealed class RefundProcessedHoldHandler(FinancialHoldEvaluator evaluator, IClock clock) : IDomainEventHandler<RefundProcessed>
    {
        private readonly FinancialHoldEvaluator _evaluator = evaluator;
        private readonly IClock _clock = clock;

        public async Task<Result> HandleAsync(RefundProcessed domainEvent, CancellationToken cancellationToken = default)
        {
            var result = await _evaluator.EvaluateStudentAsync(domainEvent.StudentId, _clock.UtcToday, cancellationToken);

            return result.IsSuccess ? Result.Ok() : Result.Fail(result.Errors);
        }
    }




}
