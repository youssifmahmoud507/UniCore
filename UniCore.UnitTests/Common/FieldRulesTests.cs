using UniCore.Domain.Common.Validation;

namespace UniCore.UnitTests.Common
{
    public class FieldRulesTests
    {
        [Theory]
        [InlineData("CS", true)]
        [InlineData("CS-101", true)]
        [InlineData("cs_101", true)]
        [InlineData("C", false)]              // too short
        [InlineData("ABCDEFGHIJKLMNOPQRSTU", false)] // 21 characters
        [InlineData("CS 101", false)]         // space
        [InlineData("CS.101", false)]         // dot
        [InlineData("", false)]
        public void IsValidCode_checks_length_and_characters(string value, bool expected)
        {
            Assert.Equal(expected, FieldRules.IsValidCode(value, FieldRules.CodeMinLength, FieldRules.CodeMaxLength));
        }

        [Fact]
        public void IsValidCode_rejects_null()
        {
            Assert.False(FieldRules.IsValidCode(null, 2, 20));
        }

        [Theory]
        [InlineData("info@uni.edu", true)]
        [InlineData("a.b@mail.uni.edu.eg", true)]
        [InlineData("no-at-sign.edu", false)]
        [InlineData("@uni.edu", false)]
        [InlineData("info@", false)]
        [InlineData("info@uni", false)]       // no dot in the domain
        [InlineData("info@.edu", false)]
        [InlineData("info@uni.", false)]
        [InlineData("a@b@uni.edu", false)]
        [InlineData("in fo@uni.edu", false)]
        public void IsValidEmail_checks_basic_shape(string value, bool expected)
        {
            Assert.Equal(expected, FieldRules.IsValidEmail(value));
        }

        [Theory]
        [InlineData("+20 100 123 4567", true)]
        [InlineData("(02) 3456-7890", true)]
        [InlineData("01001234567", true)]
        [InlineData("1234", false)]           // fewer than 5 digits
        [InlineData("12-34", false)]
        [InlineData("010abc4567", false)]
        [InlineData("20+1001234567", false)]  // "+" only allowed first
        [InlineData("123456789012345678901", false)] // longer than 20
        public void IsValidPhone_checks_characters_and_digit_count(string value, bool expected)
        {
            Assert.Equal(expected, FieldRules.IsValidPhone(value));
        }

        [Theory]
        [InlineData("https://uni.edu", true)]
        [InlineData("http://uni.edu/path?x=1", true)]
        [InlineData("ftp://uni.edu", false)]
        [InlineData("uni.edu", false)]        // not absolute
        [InlineData("javascript:alert(1)", false)]
        public void IsValidWebsite_accepts_only_absolute_http_and_https(string value, bool expected)
        {
            Assert.Equal(expected, FieldRules.IsValidWebsite(value));
        }

        [Theory]
        [InlineData("UTC", true)]
        [InlineData("Africa/Cairo", true)]
        [InlineData("Etc/GMT+2", true)]
        [InlineData("", false)]
        [InlineData("Africa Cairo", false)]
        [InlineData("Africa/Cairo;DROP", false)]
        public void IsValidTimeZoneId_checks_format_only(string value, bool expected)
        {
            Assert.Equal(expected, FieldRules.IsValidTimeZoneId(value));
        }

        [Theory]
        [InlineData(null, null)]
        [InlineData("", null)]
        [InlineData("   ", null)]
        [InlineData("  text  ", "text")]
        public void NormalizeOptional_turns_blank_into_null_and_trims(string? value, string? expected)
        {
            Assert.Equal(expected, FieldRules.NormalizeOptional(value));
        }
    }
}
