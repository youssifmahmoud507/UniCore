namespace UniCore.Domain.Modules.Attendance
{
    public sealed record AttendanceRate(int ClosedSessions, int Attended, decimal Percentage);

    // The one place that decides the percentage and the warning level.
    // Attended = Present + Excused + Late within the policy's late threshold (later than that counts as absent).
    // Percentage = attended / closed sessions x 100, rounded to 2 decimals. No closed session yet = 100.
    public static class AttendanceCalculator
    {
        public static AttendanceRate Calculate(IEnumerable<(AttendanceStatus Status, int? MinutesLate)> records, int lateThresholdMinutes)
        {
            var list = records.ToList();
            var attended = list.Count(r => r.Status switch
            {
                AttendanceStatus.Present or AttendanceStatus.Excused => true,
                AttendanceStatus.Late => (r.MinutesLate ?? 0) <= lateThresholdMinutes,
                _ => false
            });

            var percentage = list.Count == 0 ? 100m : Math.Round(attended * 100m / list.Count, 2, MidpointRounding.AwayFromZero);

            return new AttendanceRate(list.Count, attended, percentage);
        }

        // null = no warning.
        public static WarningLevel? Evaluate(decimal percentage, decimal minimumPercentage, decimal warningPercentage)
        {
            if (percentage < minimumPercentage)
            {
                return WarningLevel.Critical;
            }

            return percentage < warningPercentage ? WarningLevel.Warning : null;
        }
    }
}
