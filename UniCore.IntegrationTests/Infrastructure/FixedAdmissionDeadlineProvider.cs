using UniCore.Application.Modules.Admissions;
using UniCore.Domain.Common.Results;

namespace UniCore.IntegrationTests.Infrastructure
{
    // Test replacement of the configuration based deadline: every academic year accepts applications for a long time.
    public sealed class FixedAdmissionDeadlineProvider : IAdmissionDeadlineProvider
    {
        public Task<Result<DateTimeOffset>> GetDeadlineAsync(Guid academicYearId, CancellationToken cancellationToken = default)
            => Task.FromResult(Result<DateTimeOffset>.Ok(DateTimeOffset.UtcNow.AddYears(1)));
    }
}
