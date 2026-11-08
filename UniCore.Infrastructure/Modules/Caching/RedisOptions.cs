using StackExchange.Redis;
using System;
using System.Collections.Generic;

namespace UniCore.Infrastructure.Modules.Caching
{
    // appsettings: "Redis": { "ConnectionString": "localhost:6379,password=...", "InstanceName": "unicore:" } (all optional)
    // No connection string (the default, and the integration tests) means an in-memory cache inside the API process.
    public sealed class RedisOptions
    {
        public const string SectionName = "Redis";

        public string ConnectionString { get; set; } = string.Empty;

        // Prefix of every key, so several environments can share one Redis.
        public string InstanceName { get; set; } = "unicore:";

        public int ConnectTimeoutMs { get; set; } = 2000;

        // A slow Redis must never make the API slow: past this, the call fails and the database answers.
        public int CommandTimeoutMs { get; set; } = 1000;

        public static bool IsValid(RedisOptions options)
        {
            if (options.InstanceName is null || options.InstanceName.Length > 50)
            {
                return false;
            }

            if (options.ConnectTimeoutMs is < 100 or > 30_000 || options.CommandTimeoutMs is < 100 or > 30_000)
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(options.ConnectionString))
            {
                return true;
            }

            try
            {
                ConfigurationOptions.Parse(options.ConnectionString);
                return true;
            }
            catch (ArgumentException)
            {
                return false;
            }
            catch (FormatException)
            {
                return false;
            }
        }
    }



}
