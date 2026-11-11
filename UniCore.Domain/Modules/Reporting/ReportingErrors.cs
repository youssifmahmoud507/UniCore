using System;
using System.Collections.Generic;
using System.Text;
using UniCore.Domain.Common.Results;

namespace UniCore.Domain.Modules.Reporting
{
    public static class ReportingErrors
    {
        public static readonly Error NotFound = Error.NotFound("REPORT_NOT_FOUND", "The report does not exist.");
        public static readonly Error FormatUnsupported = Error.Validation("REPORT_FORMAT_UNSUPPORTED", "The export format must be csv, xlsx or pdf.");
        public static readonly Error DateRangeInvalid = Error.Validation("REPORT_DATE_RANGE_INVALID", "The 'from' date must not be after the 'to' date.");
        public static readonly Error DateRangeTooWide = Error.BusinessRule("REPORT_DATE_RANGE_TOO_WIDE", "The date range is too wide for this report.");
        public static readonly Error SourceTooLarge = Error.BusinessRule("REPORT_SOURCE_TOO_LARGE", "The report covers too much data. Narrow the date range.");
        public static readonly Error DataUnavailable = Error.External("REPORT_DATA_UNAVAILABLE", "The report data could not be read right now.");

        public static Error ParameterRequired(string name) => Error.Validation("REPORT_PARAMETER_REQUIRED", $"The parameter '{name}' is required.");

        public static Error ParameterInvalid(string name, string reason) => Error.Validation("REPORT_PARAMETER_INVALID", $"The parameter '{name}' {reason}.");

        public static Error ParameterUnknown(string name) => Error.Validation("REPORT_PARAMETER_UNKNOWN", $"The parameter '{name}' is not used by this report.");

        public static Error ExportTooLarge(int maxRows, string format) => Error.BusinessRule("REPORT_EXPORT_TOO_LARGE", $"The report has more than {maxRows} rows, which is the most a {format} export can hold. Narrow the filters or export CSV or Excel.");
    }
}
