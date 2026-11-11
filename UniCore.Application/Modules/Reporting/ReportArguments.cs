using System.Globalization;

namespace UniCore.Application.Modules.Reporting
{
    // The filters of one run, already checked against the report's parameters (ReportArgumentParser).
    public sealed class ReportArguments(IReadOnlyDictionary<string, object> values)
    {
        private readonly Dictionary<string, object> _values = new(values, StringComparer.OrdinalIgnoreCase);

        public static ReportArguments None => new(new Dictionary<string, object>());

        public string? Text(string name) => _values.TryGetValue(name, out var value) ? value as string : null;

        public int? Integer(string name) => _values.TryGetValue(name, out var value) && value is int number ? number : null;

        public DateOnly? Date(string name) => _values.TryGetValue(name, out var value) && value is DateOnly date ? date : null;

        public bool? Boolean(string name) => _values.TryGetValue(name, out var value) && value is bool flag ? flag : null;

        public bool Has(string name) => _values.ContainsKey(name);

        // "from=2026-01-01; status=Paid": only the filters that were given, for exports and the audit log.
        public string Summary => string.Join("; ", _values.OrderBy(v => v.Key, StringComparer.OrdinalIgnoreCase).Select(v => $"{v.Key}={Format(v.Value)}"));

        private static string Format(object value) => value switch
        {
            DateOnly date => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            bool flag => flag ? "true" : "false",
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString() ?? string.Empty
        };
    }




}
