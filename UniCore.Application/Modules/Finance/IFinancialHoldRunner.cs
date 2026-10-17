using UniCore.Domain.Common.Results;

namespace UniCore.Application.Modules.Finance
{
    // Evaluates every student. The Infrastructure runs each student in its own DI scope, so one failed save
    // cannot spoil the next student.
    public interface IFinancialHoldRunner
    {
        Task<Result<HoldEvaluationResult>> RunAsync(DateOnly today, CancellationToken cancellationToken = default);
    }




}
