namespace UniCore.Domain.Modules.Outbox
{
    public static class OutboxRetryPolicy
    {
        public const int MaxAttempts = 5;

        private static readonly TimeSpan BaseDelay = TimeSpan.FromSeconds(30);
        private static readonly TimeSpan MaxDelay = TimeSpan.FromHours(1);

        // 1st failure: 30 s, 2nd: 2 min, 3rd: 8 min, 4th: 32 min (capped at 1 hour).
        public static TimeSpan DelayAfter(int failedAttempts)
        {
            var exponent = Math.Clamp(failedAttempts - 1, 0, 10);
            var delay = TimeSpan.FromTicks(BaseDelay.Ticks * (long)Math.Pow(4, exponent));

            return delay > MaxDelay ? MaxDelay : delay;
        }
    }



}
