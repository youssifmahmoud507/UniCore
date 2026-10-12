using System;
using System.Collections.Generic;
using System.Text;

namespace UniCore.Infrastructure.Modules.Students
{
    // One row per (year, program code). Incremented atomically by StudentNumberGenerator.
    public sealed class StudentNumberCounter
    {
        public string CounterKey { get; set; } = string.Empty;

        public int LastValue { get; set; }
    }
}
