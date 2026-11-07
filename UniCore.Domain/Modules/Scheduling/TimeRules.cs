namespace UniCore.Domain.Modules.Scheduling
{
    // The one place that decides what a valid time range is and when two ranges overlap.
    public static class TimeRules
    {
        public static readonly TimeSpan MinDuration = TimeSpan.FromMinutes(30);
        public static readonly TimeSpan MaxSlotDuration = TimeSpan.FromHours(8);

        // Whole minutes, end after start, at least 30 minutes.
        public static bool IsValidWindow(TimeOnly start, TimeOnly end)
            => start.Ticks % TimeSpan.TicksPerMinute == 0
               && end.Ticks % TimeSpan.TicksPerMinute == 0
               && end > start
               && end - start >= MinDuration;

        public static bool IsValidSlot(TimeOnly start, TimeOnly end)
            => IsValidWindow(start, end) && end - start <= MaxSlotDuration;

        // Touching ranges (one ends when the other starts) do not overlap.
        public static bool Overlaps(TimeOnly startA, TimeOnly endA, TimeOnly startB, TimeOnly endB)
            => startA < endB && startB < endA;

        // The inner range lies fully inside the outer one.
        public static bool Contains(TimeOnly outerStart, TimeOnly outerEnd, TimeOnly innerStart, TimeOnly innerEnd)
            => outerStart <= innerStart && innerEnd <= outerEnd;
    }
}
