using System;
using System.Collections.Generic;
using System.Text;

namespace UniCore.Application.Common.Abstractions
{
    public interface IClock
    {
        DateTimeOffset UtcNow { get; }
        DateOnly UtcToday { get; }
    }
}
