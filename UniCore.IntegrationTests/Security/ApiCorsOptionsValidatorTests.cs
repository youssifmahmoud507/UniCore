using System;
using System.Collections.Generic;
using System.Text;
using UniCore.Api.Security;

namespace UniCore.IntegrationTests.Security
{
    public class ApiCorsOptionsValidatorTests
    {
        private readonly ApiCorsOptionsValidator _validator = new();

        [Fact]
        public void Default_options_allow_nobody_and_pass()
            => Assert.True(_validator.Validate(null, new ApiCorsOptions()).Succeeded);

        [Theory]
        [InlineData("https://app.example.com")]
        [InlineData("https://app.example.com:8443")]
        [InlineData("http://localhost:3000")]
        [InlineData("http://127.0.0.1:5173")]
        public void Plain_origins_pass(string origin)
            => Assert.True(_validator.Validate(null, new ApiCorsOptions { AllowedOrigins = [origin] }).Succeeded);

        [Theory]
        [InlineData("*")]
        [InlineData("https://*.example.com")]
        [InlineData("https://app.example.com/")]
        [InlineData("https://app.example.com/path")]
        [InlineData("https://app.example.com?x=1")]
        [InlineData("https://app.example.com#f")]
        [InlineData("https://user@app.example.com")]
        [InlineData("http://app.example.com")]
        [InlineData("ftp://app.example.com")]
        [InlineData("app.example.com")]
        [InlineData(" https://app.example.com")]
        [InlineData("null")]
        [InlineData("")]
        public void Wide_or_malformed_origins_fail(string origin)
            => Assert.True(_validator.Validate(null, new ApiCorsOptions { AllowedOrigins = [origin] }).Failed);

        [Theory]
        [InlineData(0, true)]
        [InlineData(1440, true)]
        [InlineData(-1, false)]
        [InlineData(1441, false)]
        public void Preflight_age_must_be_in_range(int minutes, bool valid)
            => Assert.Equal(valid, _validator.Validate(null, new ApiCorsOptions { PreflightMaxAgeMinutes = minutes }).Succeeded);
    }
}
