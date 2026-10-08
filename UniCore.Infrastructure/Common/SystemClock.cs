using System;
using System.Collections.Generic;
using System.Text;
using UniCore.Application.Common.Abstractions;

namespace UniCore.Infrastructure.Common
{
    public sealed class SystemClock : IClock
    {
        public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
        public DateOnly UtcToday => DateOnly.FromDateTime(DateTime.UtcNow);
    }
}
