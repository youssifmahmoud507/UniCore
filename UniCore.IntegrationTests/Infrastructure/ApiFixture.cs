using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;
using Testcontainers.MsSql;
using UniCore.Infrastructure.Persistence;

namespace UniCore.IntegrationTests.Infrastructure
{
    // One SQL Server container and one running API for the whole test run.
    public sealed class ApiFixture : IAsyncLifetime
    {
        public const string AdminLogin = "admin";
        public const string AdminEmail = "admin@test.local";
        public const string AdminPassword = "TestAdmin#2026xK9";
        public static string DocumentsRoot { get; private set; } = string.Empty;


        private MsSqlContainer? _container;
        private UniCoreApiFactory? _factory;
        private TestApi? _api;

        public HttpClient CreateHttpClient() => _factory.CreateClient();

        public HttpMessageHandler CreateHandler() => _factory.Server.CreateHandler();

        public TestApi Api
        {
            get
            {
                Assert.NotNull(_api);
                return _api;
            }
        }

        public IServiceProvider Services
        {
            get
            {
                Assert.NotNull(_factory);
                return _factory.Services;
            }
        }

        public async Task InitializeAsync()
        {
            _container = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();
            await _container.StartAsync();

            var connectionString = new SqlConnectionStringBuilder(_container.GetConnectionString())
            {
                InitialCatalog = "UniCoreTests"
            }.ConnectionString;

            // Migrate first: the API seeds roles and the admin while it starts.
            var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(connectionString).Options;

            await using (var db = new AppDbContext(options))
            {
                await db.Database.MigrateAsync();
            }

            // Environment variables beat user-secrets and appsettings, so this works on any machine and in CI.
            Set("ConnectionStrings__DefaultConnection", connectionString);
            Set("Jwt__SigningKey", "integration-tests-signing-key-integration-tests-0123456789");
            Set("PasswordReset__Pepper", "integration-tests-pepper-integration-tests-0123456789");
            Set("PasswordReset__ResendCooldownSeconds", "0");
            Set("SeedAdmin__UserName", AdminLogin);
            Set("SeedAdmin__Email", AdminEmail);
            Set("SeedAdmin__Password", AdminPassword);
            Set("RateLimiting__Disabled", "true");

            DocumentsRoot = Path.Combine(Path.GetTempPath(), "unicore-tests-docs-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(DocumentsRoot);
            Set("Documents__StoragePath", DocumentsRoot);
            Set("Documents__MaxFileSizeBytes", "1048576");

            _factory = new UniCoreApiFactory();
            var client = _factory.CreateClient(); // starts the API

            _api = new TestApi(new ApiClient(client), _factory.Services.GetRequiredService<CapturingEmailSender>());
        }

        public async Task DisposeAsync()
        {
            _factory?.Dispose();

            if (_container is not null)
            {
                await _container.DisposeAsync();
            }
            try { Directory.Delete(DocumentsRoot, recursive: true); } catch (IOException) { }
        }

        private static void Set(string name, string value) => Environment.SetEnvironmentVariable(name, value);
    }


    public sealed record RawResult(int Code, byte[] Bytes, string? ContentType, string? Disposition, string? NoSniff, JsonElement Json)
    {
        public string? Str(string name) => Json.ValueKind == JsonValueKind.Object && Json.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

        public string? ErrorCode => Str("errorCode");

        public JsonElement Prop(string name) => Json.GetProperty(name);
    }

}
