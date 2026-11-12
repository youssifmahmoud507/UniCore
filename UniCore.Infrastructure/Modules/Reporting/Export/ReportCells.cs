using System;
using System.Collections.Generic;
using System.Globalization;
using UniCore.Application.Modules.Reporting;

namespace UniCore.Infrastructure.Modules.Reporting.Export
{
    // How a cell value is written as text, the same way in every format that writes text.
    public static class ReportCells
    {
        public static string Text(object? value) => value switch
        {
            null => string.Empty,
            string text => text,
            bool flag => flag ? "true" : "false",
            decimal number => number.ToString("0.00", CultureInfo.InvariantCulture),
            int or long or short or byte => Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty,
            double number => number.ToString("0.##", CultureInfo.InvariantCulture),
            DateOnly date => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            DateTimeOffset moment => moment.UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
            DateTime moment => moment.ToUniversalTime().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
            _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty
        };

        public static string Title(ReportExportModel model) => model.Title;
    }




}
