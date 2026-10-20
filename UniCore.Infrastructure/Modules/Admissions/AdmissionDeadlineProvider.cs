using System.Globalization;
using Microsoft.Extensions.Configuration;
using UniCore.Application.Modules.Admissions;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Admissions;

namespace UniCore.Infrastructure.Modules.Admissions
{
    // Temporary source of the admission deadline until the Calendar module (Week 4) stores it per academic year.
    // appsettings:  "Admissions": { "Deadlines": { "<academicYearId>": "2027-08-31T23:59:59+03:00" } }
    // A year that is not listed (or has an unreadable date) returns ADMISSION_DEADLINE_NOT_CONFIGURED.
    public sealed class AdmissionDeadlineProvider(IConfiguration configuration) : IAdmissionDeadlineProvider
    {
        public const string SectionKey = "Admissions:Deadlines";

        private readonly IConfiguration _configuration = configuration;

        public Task<Result<DateTimeOffset>> GetDeadlineAsync(Guid academicYearId, CancellationToken cancellationToken = default)
        {
            var text = _configuration.GetSection(SectionKey)[academicYearId.ToString()];

            if (string.IsNullOrWhiteSpace(text)
                || !DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var deadline))
            {
                return Task.FromResult(Result<DateTimeOffset>.Fail(AdmissionsErrors.DeadlineNotConfigured));
            }

            return Task.FromResult(Result<DateTimeOffset>.Ok(deadline));
        }
    }
}
