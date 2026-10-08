using System;
using System.Collections.Generic;
using System.Text;
using UniCore.Application.Common.Abstractions;

namespace UniCore.UnitTests.Common
{
    public sealed class FakeClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset UtcNow { get; private set; } = now;
        public DateOnly UtcToday => DateOnly.FromDateTime(UtcNow.UtcDateTime);

        public void Advance(TimeSpan by) => UtcNow = UtcNow.Add(by);



    }
}
