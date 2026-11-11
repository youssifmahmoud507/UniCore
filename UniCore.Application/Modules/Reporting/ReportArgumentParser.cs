using System.Globalization;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Reporting;

namespace UniCore.Application.Modules.Reporting
{
    // Turns the query string of a request into typed arguments, or says exactly what is wrong. Unknown names are
    // refused so a typo never silently runs the report without its filter.
    public static class ReportArgumentParser
    {
        public const int MaxTextLength = 100;

        public static Result<ReportArguments> Parse(ReportDescriptor descriptor, IReadOnlyDictionary<string, string?> raw)
        {
            var errors = new List<Error>();
            var values = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

            foreach (var name in raw.Keys)
            {
                if (descriptor.Parameters.All(p => !string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase)))
                {
                    errors.Add(ReportingErrors.ParameterUnknown(name));
                }
            }

            foreach (var parameter in descriptor.Parameters)
            {
                var text = raw.FirstOrDefault(r => string.Equals(r.Key, parameter.Name, StringComparison.OrdinalIgnoreCase)).Value?.Trim();

                if (string.IsNullOrEmpty(text))
                {
                    if (parameter.Required)
                    {
                        errors.Add(ReportingErrors.ParameterRequired(parameter.Name));
                    }

                    continue;
                }

                var parsed = ParseValue(parameter, text);

                if (parsed.TryGetValue(out var value))
                {
                    values[parameter.Name] = value!;
                }
                else
                {
                    errors.AddRange(parsed.Errors);
                }
            }

            return errors.Count == 0 ? new ReportArguments(values) : Result<ReportArguments>.Fail(errors);
        }

        private static Result<object> ParseValue(ReportParameter parameter, string text)
        {
            switch (parameter.Type)
            {
                case ReportParameterType.Text:
                    return text.Length <= MaxTextLength
                        ? Result<object>.Ok(text)
                        : ReportingErrors.ParameterInvalid(parameter.Name, $"must be at most {MaxTextLength} characters");

                case ReportParameterType.Integer:
                    return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number)
                        ? Result<object>.Ok(number)
                        : ReportingErrors.ParameterInvalid(parameter.Name, "must be a whole number");

                case ReportParameterType.Date:
                    return DateOnly.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
                        ? Result<object>.Ok(date)
                        : ReportingErrors.ParameterInvalid(parameter.Name, "must be a date like 2026-09-30");

                case ReportParameterType.Boolean:
                    return bool.TryParse(text, out var flag)
                        ? Result<object>.Ok(flag)
                        : ReportingErrors.ParameterInvalid(parameter.Name, "must be true or false");

                case ReportParameterType.Choice:
                    var choice = parameter.Choices?.FirstOrDefault(c => string.Equals(c, text, StringComparison.OrdinalIgnoreCase));

                    return choice is not null
                        ? Result<object>.Ok(choice)
                        : ReportingErrors.ParameterInvalid(parameter.Name, $"must be one of: {string.Join(", ", parameter.Choices ?? [])}");

                default:
                    return ReportingErrors.ParameterInvalid(parameter.Name, "has an unsupported type");
            }
        }
    }




}
