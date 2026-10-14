using UniCore.Application.Common.Abstractions;

namespace UniCore.IntegrationTests.Infrastructure
{
    // A clock a test can move forward, to step through retry delays without waiting.
    public sealed class MutableClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset UtcNow { get; private set; } = now;

        public DateOnly UtcToday => DateOnly.FromDateTime(UtcNow.UtcDateTime);

        public void Advance(TimeSpan by) => UtcNow = UtcNow.Add(by);
    }

}
