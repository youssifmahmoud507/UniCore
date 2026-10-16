using Microsoft.Extensions.Configuration;
using UniCore.Application.Common.Abstractions;
using UniCore.Application.Modules.Calendar;

namespace UniCore.Infrastructure.Modules.Calendar
{
    // "Today" in the university time zone. The zone id comes from configuration (University:TimeZone,
    // default Africa/Cairo). An unknown id falls back to UTC instead of failing every request;
    // academic-lifecycle.md open question 2 asks whether this should become a system setting.
    public sealed class AcademicClock : IAcademicClock
    {
        public const string DefaultTimeZoneId = "Africa/Cairo";

        private readonly IClock _clock;
        private readonly TimeZoneInfo _zone;

        public AcademicClock(IClock clock, IConfiguration configuration)
        {
            _clock = clock;

            var id = configuration["University:TimeZone"];
            _zone = TimeZoneInfo.TryFindSystemTimeZoneById(string.IsNullOrWhiteSpace(id) ? DefaultTimeZoneId : id, out var found)
                ? found
                : TimeZoneInfo.Utc;
        }

        public DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(_clock.UtcNow, _zone).DateTime);
    }
}
