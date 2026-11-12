using UniCore.Application.Modules.Reporting;

namespace UniCore.UnitTests.Modules.Reporting
{
    public class ReportArgumentParserTests
    {
        private static readonly ReportDescriptor Descriptor = new(
            "TEST",
            "Test",
            "Test",
            "Test",
            "Reports.Read",
            false,
            [
                new("from", "From", ReportParameterType.Date),
                new("count", "Count", ReportParameterType.Integer),
                new("flag", "Flag", ReportParameterType.Boolean),
                new("status", "Status", ReportParameterType.Choice, false, ["Open", "Closed"]),
                new("name", "Name", ReportParameterType.Text),
                new("must", "Must", ReportParameterType.Text, Required: true)
            ],
            []);

        private static Dictionary<string, string?> Raw(params (string Key, string? Value)[] pairs)
            => pairs.ToDictionary(p => p.Key, p => p.Value, StringComparer.OrdinalIgnoreCase);

        [Fact]
        public void Valid_values_are_parsed_to_their_types()
        {
            var parsed = ReportArgumentParser.Parse(Descriptor, Raw(("from", "2026-09-30"), ("count", "12"), ("flag", "true"), ("status", "closed"), ("name", " Ali "), ("must", "x")));

            Assert.True(parsed.TryGetValue(out var arguments));
            Assert.NotNull(arguments);
            Assert.Equal(new DateOnly(2026, 9, 30), arguments.Date("from"));
            Assert.Equal(12, arguments.Integer("count"));
            Assert.Equal(true, arguments.Boolean("flag"));
            Assert.Equal("Closed", arguments.Text("status"));
            Assert.Equal("Ali", arguments.Text("name"));
        }

        [Fact]
        public void A_choice_is_matched_in_any_letter_case_and_returned_in_the_canonical_form()
        {
            var parsed = ReportArgumentParser.Parse(Descriptor, Raw(("status", "oPeN"), ("must", "x")));

            Assert.True(parsed.TryGetValue(out var arguments));
            Assert.Equal("Open", arguments!.Text("status"));
        }

        [Fact]
        public void Parameter_names_are_not_case_sensitive()
        {
            var parsed = ReportArgumentParser.Parse(Descriptor, Raw(("FROM", "2026-01-02"), ("Must", "x")));

            Assert.True(parsed.TryGetValue(out var arguments));
            Assert.Equal(new DateOnly(2026, 1, 2), arguments!.Date("from"));
        }

        [Fact]
        public void Optional_values_that_are_missing_or_blank_are_left_out()
        {
            var parsed = ReportArgumentParser.Parse(Descriptor, Raw(("from", "  "), ("count", ""), ("must", "x")));

            Assert.True(parsed.TryGetValue(out var arguments));
            Assert.False(arguments!.Has("from"));
            Assert.Null(arguments.Integer("count"));
            Assert.Equal("must=x", arguments.Summary);
        }

        [Fact]
        public void A_required_parameter_that_is_missing_is_refused()
        {
            var parsed = ReportArgumentParser.Parse(Descriptor, Raw());

            Assert.True(parsed.IsFailure);
            Assert.Equal("REPORT_PARAMETER_REQUIRED", parsed.Errors[0].Code);
        }

        [Theory]
        [InlineData("from", "30/09/2026")]
        [InlineData("from", "2026-13-01")]
        [InlineData("from", "2026-9-3")]
        [InlineData("count", "ten")]
        [InlineData("count", "99999999999")]
        [InlineData("count", "1.5")]
        [InlineData("flag", "yes")]
        [InlineData("status", "Pending")]
        public void A_value_of_the_wrong_kind_is_refused_with_the_parameter_name(string name, string value)
        {
            var parsed = ReportArgumentParser.Parse(Descriptor, Raw((name, value), ("must", "x")));

            Assert.True(parsed.IsFailure);
            Assert.Equal("REPORT_PARAMETER_INVALID", parsed.Errors[0].Code);
            Assert.Contains(name, parsed.Errors[0].Description);
        }

        [Fact]
        public void A_text_longer_than_100_characters_is_refused()
        {
            var parsed = ReportArgumentParser.Parse(Descriptor, Raw(("name", new string('a', ReportArgumentParser.MaxTextLength + 1)), ("must", "x")));

            Assert.Equal("REPORT_PARAMETER_INVALID", parsed.Errors[0].Code);
        }

        [Fact]
        public void An_unknown_parameter_is_refused_so_a_typo_never_runs_the_report_unfiltered()
        {
            var parsed = ReportArgumentParser.Parse(Descriptor, Raw(("frm", "2026-01-01"), ("must", "x")));

            Assert.True(parsed.IsFailure);
            Assert.Equal("REPORT_PARAMETER_UNKNOWN", parsed.Errors[0].Code);
            Assert.Contains("frm", parsed.Errors[0].Description);
        }

        [Fact]
        public void Every_problem_is_reported_at_once()
        {
            var parsed = ReportArgumentParser.Parse(Descriptor, Raw(("from", "x"), ("count", "y"), ("other", "z")));

            Assert.True(parsed.IsFailure);
            Assert.Equal(4, parsed.Errors.Count);
        }

        [Fact]
        public void The_summary_lists_only_the_given_filters_in_a_stable_order()
        {
            var parsed = ReportArgumentParser.Parse(Descriptor, Raw(("status", "Open"), ("from", "2026-01-05"), ("flag", "false"), ("count", "3"), ("must", "m")));

            Assert.True(parsed.TryGetValue(out var arguments));
            Assert.Equal("count=3; flag=false; from=2026-01-05; must=m; status=Open", arguments!.Summary);
        }
    }




}
