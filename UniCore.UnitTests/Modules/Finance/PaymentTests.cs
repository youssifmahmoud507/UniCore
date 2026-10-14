using UniCore.Domain.Modules.Finance;

namespace UniCore.UnitTests.Modules.Finance
{
    public class PaymentTests
    {
        [Theory]
        [InlineData("abcdefgh", "abcdefgh")]
        [InlineData("  key-12345678  ", "key-12345678")]
        [InlineData("3f2b9c1e-7d4a-4b6e-9a1c-0d5e8f7a6b5c", "3f2b9c1e-7d4a-4b6e-9a1c-0d5e8f7a6b5c")]
        public void A_valid_key_is_trimmed_and_kept(string input, string expected)
        {
            Assert.Equal(expected, Payment.NormalizeKey(input));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("short")]
        [InlineData("has space inside")]
        [InlineData("tab\tinside-key")]
        [InlineData("accént-key-123")]
        public void A_missing_short_or_odd_key_is_invalid(string? input)
        {
            Assert.Null(Payment.NormalizeKey(input));
        }

        [Fact]
        public void A_key_longer_than_the_limit_is_invalid()
        {
            Assert.Null(Payment.NormalizeKey(new string('k', Payment.MaxKeyLength + 1)));
            Assert.NotNull(Payment.NormalizeKey(new string('k', Payment.MaxKeyLength)));
        }
    }

}
