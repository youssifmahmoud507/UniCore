using System;
using System.Collections.Generic;
using UniCore.Infrastructure.Modules.Idempotency;

namespace UniCore.IntegrationTests.Idempotency
{
    public class IdempotencyOptionsTests
    {
        [Fact]
        public void The_defaults_are_24_hours_and_two_minutes_and_are_valid()
        {
            var options = new IdempotencyOptions();

            Assert.Equal(24, options.ExpiryHours);
            Assert.Equal(120, options.InProgressTimeoutSeconds);
            Assert.True(IdempotencyOptions.IsValid(options));
        }

        [Theory]
        [InlineData(1, 10, true)]
        [InlineData(168, 3600, true)]
        [InlineData(0, 120, false)]
        [InlineData(169, 120, false)]
        [InlineData(24, 9, false)]
        [InlineData(24, 3601, false)]
        public void Values_must_stay_in_range(int hours, int seconds, bool valid)
            => Assert.Equal(valid, IdempotencyOptions.IsValid(new IdempotencyOptions { ExpiryHours = hours, InProgressTimeoutSeconds = seconds }));
    }
}
