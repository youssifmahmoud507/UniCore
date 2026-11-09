using Microsoft.Extensions.Logging;
using UniCore.Application.Common.Abstractions;
using UniCore.Application.Common.Repositories;
using UniCore.Application.Modules.Grading.Specifications;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Grading;

namespace UniCore.Infrastructure.Modules.Grading
{
    // Creates the standard 4.0 grade scale if there is no default scale yet. Safe to run on every start.
    public sealed class GradeScaleSeeder(IAggregateRepository<GradeScale> scales, IUnitOfWork unitOfWork, IClock clock, ILogger<GradeScaleSeeder> logger)
    {
        private readonly IAggregateRepository<GradeScale> _scales = scales;
        private readonly IUnitOfWork _unitOfWork = unitOfWork;
        private readonly IClock _clock = clock;
        private readonly ILogger<GradeScaleSeeder> _logger = logger;

        public async Task<Result> SeedAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                if (await _scales.AnyAsync(new DefaultGradeScaleSpecification(), cancellationToken)
                    || await _scales.AnyAsync(new GradeScaleByNameSpecification(GradeScale.StandardName), cancellationToken))
                {
                    return Result.Ok();
                }

                var created = GradeScale.Create(GradeScale.StandardName, [.. GradeScale.StandardBands], isDefault: true, _clock.UtcNow);
                if (!created.TryGetValue(out var scale))
                {
                    return Result.Fail(created.Errors);
                }

                _scales.Add(scale);
                var saved = await _unitOfWork.SaveChangesAsync(cancellationToken);

                return saved.IsSuccess ? Result.Ok() : Result.Fail(saved.Errors);
            }
            catch (OperationCanceledException)
            {
                return Result.Fail(CommonErrors.OperationCancelled);
            }
            catch (Exception ex)
            {
                _logger.LogError("Seeding the grade scale failed ({ExceptionType}).", ex.GetType().Name);

                return Result.Fail(CommonErrors.ExternalServiceUnavailable);
            }
        }
    }
}
