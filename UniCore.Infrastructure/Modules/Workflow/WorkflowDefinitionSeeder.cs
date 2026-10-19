using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;
using UniCore.Application.Common.Abstractions;
using UniCore.Application.Common.Repositories;
using UniCore.Application.Modules.Workflow;
using UniCore.Application.Modules.Workflow.Specifications;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Workflow;

namespace UniCore.Infrastructure.Modules.Workflow
{
    // Creates the standard definitions that do not exist yet. Safe to run on every start: a code that exists is left alone.
    public sealed class WorkflowDefinitionSeeder(IAggregateRepository<WorkflowDefinition> definitions, IUnitOfWork unitOfWork, IClock clock, ILogger<WorkflowDefinitionSeeder> logger)
    {
        private readonly IAggregateRepository<WorkflowDefinition> _definitions = definitions;
        private readonly IUnitOfWork _unitOfWork = unitOfWork;
        private readonly IClock _clock = clock;
        private readonly ILogger<WorkflowDefinitionSeeder> _logger = logger;

        public async Task<Result> SeedAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var added = 0;

                foreach (var seed in WorkflowSeedCatalog.Definitions)
                {
                    if (await _definitions.AnyAsync(new WorkflowDefinitionsByCodeSpecification(seed.Code), cancellationToken))
                    {
                        continue;
                    }

                    var created = WorkflowDefinition.Create(seed.Code, seed.Name, 1, seed.Steps, _clock.UtcNow);
                    if (!created.TryGetValue(out var definition))
                    {
                        return Result.Fail(created.Errors);
                    }

                    _definitions.Add(definition);
                    added++;
                }

                if (added == 0)
                {
                    return Result.Ok();
                }

                var saved = await _unitOfWork.SaveChangesAsync(cancellationToken);

                return saved.IsSuccess ? Result.Ok() : Result.Fail(saved.Errors);
            }
            catch (OperationCanceledException)
            {
                return Result.Fail(CommonErrors.OperationCancelled);
            }
            catch (Exception ex)
            {
                _logger.LogError("Seeding workflow definitions failed ({ExceptionType}).", ex.GetType().Name);
                return Result.Fail(CommonErrors.ExternalServiceUnavailable);
            }
        }
    }
}
