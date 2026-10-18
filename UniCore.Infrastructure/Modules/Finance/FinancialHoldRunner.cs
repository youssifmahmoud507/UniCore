using Microsoft.Extensions.DependencyInjection;
using UniCore.Application.Modules.Finance;
using UniCore.Domain.Common.Results;

namespace UniCore.Infrastructure.Modules.Finance
{
    // Every student gets its own DI scope, so its own DbContext: a failed save for one student (a duplicate, a
    // concurrency conflict) leaves nothing behind that could spoil the next one.
    public sealed class FinancialHoldRunner(IServiceScopeFactory scopes) : IFinancialHoldRunner
    {
        private readonly IServiceScopeFactory _scopes = scopes;

        public async Task<Result<HoldEvaluationResult>> RunAsync(DateOnly today, CancellationToken cancellationToken = default)
        {
            IReadOnlyList<HoldCandidate> candidates;

            using (var scope = _scopes.CreateScope())
            {
                var evaluator = scope.ServiceProvider.GetRequiredService<FinancialHoldEvaluator>();

                var found = await evaluator.FindCandidatesAsync(today, cancellationToken);
                if (!found.TryGetValue(out var list))
                {
                    return Result<HoldEvaluationResult>.Fail(found.Errors);
                }

                candidates = list;
            }

            var placed = 0;
            var released = 0;
            var failed = 0;

            foreach (var candidate in candidates)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                using var scope = _scopes.CreateScope();
                var evaluator = scope.ServiceProvider.GetRequiredService<FinancialHoldEvaluator>();

                var applied = await evaluator.ApplyAsync(candidate.StudentId, candidate.Overdue, cancellationToken);

                if (applied.TryGetValue(out var outcome))
                {
                    placed += outcome.Placed;
                    released += outcome.Released;
                }
                else
                {
                    failed++;
                }
            }

            return new HoldEvaluationResult(placed, released, failed);
        }
    }



}
