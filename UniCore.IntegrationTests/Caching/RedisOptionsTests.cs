using UniCore.Infrastructure.Modules.Caching;

namespace UniCore.IntegrationTests.Caching
{
    public class RedisOptionsTests
    {
        [Fact]
        public void The_defaults_are_valid_and_mean_an_in_memory_cache()
        {
            var options = new RedisOptions();

            Assert.True(RedisOptions.IsValid(options));
            Assert.Equal(string.Empty, options.ConnectionString);
        }

        [Theory]
        [InlineData("localhost:6379")]
        [InlineData("redis:6379,password=secret,ssl=false")]
        public void A_connection_string_passes(string connectionString)
            => Assert.True(RedisOptions.IsValid(new RedisOptions { ConnectionString = connectionString }));

        [Theory]
        [InlineData("localhost:6379,unknownoption=1")]
        public void A_malformed_connection_string_fails(string connectionString)
            => Assert.False(RedisOptions.IsValid(new RedisOptions { ConnectionString = connectionString }));

        [Theory]
        [InlineData(99, 1000, false)]
        [InlineData(100, 1000, true)]
        [InlineData(2000, 30_000, true)]
        [InlineData(2000, 30_001, false)]
        [InlineData(2000, 50, false)]
        public void Timeouts_must_be_between_100_and_30000_ms(int connect, int command, bool valid)
            => Assert.Equal(valid, RedisOptions.IsValid(new RedisOptions { ConnectTimeoutMs = connect, CommandTimeoutMs = command }));

        [Fact]
        public void The_instance_name_is_limited_to_50_characters()
        {
            Assert.True(RedisOptions.IsValid(new RedisOptions { InstanceName = new string('i', 50) }));
            Assert.False(RedisOptions.IsValid(new RedisOptions { InstanceName = new string('i', 51) }));
        }
    }
}
