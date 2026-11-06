using UniCore.Api.Security;

namespace UniCore.IntegrationTests.Security
{
    public class ProxyOptionsTests
    {
        [Fact]
        public void No_trusted_networks_is_valid_and_means_forwarded_headers_are_ignored()
            => Assert.True(ProxyOptions.IsValid(new ProxyOptions()));

        [Theory]
        [InlineData("10.0.0.0/8")]
        [InlineData("172.18.0.5")]
        [InlineData("::1")]
        [InlineData("fd00::/8")]
        public void Addresses_and_ranges_pass(string entry)
            => Assert.True(ProxyOptions.IsValid(new ProxyOptions { TrustedNetworks = [entry] }));

        [Theory]
        [InlineData("")]
        [InlineData(" ")]
        [InlineData("not-an-ip")]
        [InlineData("10.0.0.0/99")]
        [InlineData("10.0.0.0/")]
        [InlineData("10.0.0.0/8 ")]
        public void Anything_else_fails(string entry)
            => Assert.False(ProxyOptions.IsValid(new ProxyOptions { TrustedNetworks = [entry] }));
    }
}
