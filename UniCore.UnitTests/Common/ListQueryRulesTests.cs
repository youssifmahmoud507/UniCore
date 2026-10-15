using UniCore.Application.Common.Sorting;

namespace UniCore.UnitTests.Common
{
    public class ListQueryRulesTests
    {
        private static readonly string[] Allowed = ["code", "name", "createdAt"];

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void ParseSort_blank_gives_the_default_field_ascending(string? sort)
        {
            var option = ResultAssert.Succeeded(ListQueryRules.ParseSort(sort, Allowed, "code"));

            Assert.Equal("code", option.Field);
            Assert.False(option.Descending);
        }

        [Fact]
        public void ParseSort_reads_the_direction_from_the_minus_prefix()
        {
            var ascending = ResultAssert.Succeeded(ListQueryRules.ParseSort("name", Allowed, "code"));
            var descending = ResultAssert.Succeeded(ListQueryRules.ParseSort("-name", Allowed, "code"));

            Assert.Equal("name", ascending.Field);
            Assert.False(ascending.Descending);
            Assert.Equal("name", descending.Field);
            Assert.True(descending.Descending);
        }

        [Fact]
        public void ParseSort_ignores_case_and_returns_the_whitelist_spelling()
        {
            var option = ResultAssert.Succeeded(ListQueryRules.ParseSort(" -CREATEDAT ", Allowed, "code"));

            Assert.Equal("createdAt", option.Field);
            Assert.True(option.Descending);
        }

        [Theory]
        [InlineData("password")]
        [InlineData("-id")]
        [InlineData("name;drop table")]
        [InlineData("-")]
        public void ParseSort_rejects_fields_outside_the_whitelist(string sort)
        {
            ResultAssert.Failed(ListQueryRules.ParseSort(sort, Allowed, "code"), ListQueryRules.SortFieldInvalid);
        }

        [Fact]
        public void ParseSort_rejects_several_fields_for_now()
        {
            ResultAssert.Failed(ListQueryRules.ParseSort("name,-code", Allowed, "code"), ListQueryRules.SortMultipleNotSupported);
        }

        [Theory]
        [InlineData(null, null)]
        [InlineData("", null)]
        [InlineData("   ", null)]
        [InlineData("  cairo ", "cairo")]
        public void NormalizeSearch_trims_and_turns_blank_into_null(string? search, string? expected)
        {
            Assert.Equal(expected, ListQueryRules.NormalizeSearch(search));
        }

        [Fact]
        public void NormalizeSearch_cuts_very_long_text()
        {
            var result = ListQueryRules.NormalizeSearch(new string('a', 500));

            Assert.Equal(ListQueryRules.MaxSearchLength, result?.Length);
        }
    }
}
