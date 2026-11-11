using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Reporting;

namespace UniCore.Application.Modules.Reporting
{
    // The from/to filters every dated report shares.
    public static class ReportRanges
    {
        // Both ends optional. from must not be after to; maxDays (when given) limits the span, both days included.
        public static Result<(DateOnly? From, DateOnly? To)> Read(ReportArguments arguments, int? maxDays = null)
        {
            var from = arguments.Date("from");
            var to = arguments.Date("to");

            if (from is { } start && to is { } end)
            {
                if (start > end)
                {
                    return ReportingErrors.DateRangeInvalid;
                }

                if (maxDays is { } limit && end.DayNumber - start.DayNumber + 1 > limit)
                {
                    return ReportingErrors.DateRangeTooWide;
                }
            }

            return (from, to);
        }

        // For reports that always need a range: a missing end falls back to the last "days" days ending today.
        public static Result<(DateOnly From, DateOnly To)> ReadWithDefault(ReportArguments arguments, DateOnly today, int defaultDays, int maxDays)
        {
            var to = arguments.Date("to") ?? today;
            var from = arguments.Date("from") ?? to.AddDays(-(defaultDays - 1));

            if (from > to)
            {
                return ReportingErrors.DateRangeInvalid;
            }

            if (to.DayNumber - from.DayNumber + 1 > maxDays)
            {
                return ReportingErrors.DateRangeTooWide;
            }

            return (from, to);
        }

        public static DateTimeOffset StartOf(DateOnly day) => new(day.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);

        public static DateTimeOffset StartOfNext(DateOnly day) => StartOf(day.AddDays(1));
    }




}
