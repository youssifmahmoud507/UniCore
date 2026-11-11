using System;
using System.Collections.Generic;
using System.Text;

namespace UniCore.Application.Modules.Reporting.Dashboard
{
    public sealed record DashboardMetric(string Key, string Label, decimal Value, string? Unit = null);


}
