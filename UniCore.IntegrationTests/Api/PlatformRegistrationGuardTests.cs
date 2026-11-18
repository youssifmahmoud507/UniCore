using Microsoft.Extensions.DependencyInjection;
using UniCore.Application.Common.Abstractions;
using UniCore.Application.Modules.Idempotency;
using UniCore.Application.Modules.Identity;
using UniCore.Application.Modules.Reporting;
using UniCore.Application.Modules.Reporting.Dashboard;
using UniCore.Infrastructure.Modules.Caching;
using UniCore.IntegrationTests.Infrastructure;

namespace UniCore.IntegrationTests.Api
{
    // Regression guard: a merge that silently drops platform registrations (like the DI revert incident)
    // will fail these tests. They resolve services from the REAL DI container built by WebApplicationFactory.
    [Collection(ApiCollection.Name)]
    public sealed class PlatformRegistrationGuardTests(ApiFixture fixture)
    {
        [Fact]
        public void ICacheService_is_registered()
        {
            using var scope = fixture.Services.CreateScope();
            var service = scope.ServiceProvider.GetService<ICacheService>();

            Assert.NotNull(service);
        }

        [Fact]
        public void IIdempotencyService_is_registered()
        {
            using var scope = fixture.Services.CreateScope();
            var service = scope.ServiceProvider.GetService<IIdempotencyService>();

            Assert.NotNull(service);
        }

        [Fact]
        public void IAuditWriter_is_registered()
        {
            using var scope = fixture.Services.CreateScope();
            var service = scope.ServiceProvider.GetService<IAuditWriter>();

            Assert.NotNull(service);
        }

        [Fact]
        public void IUserAccessProvider_is_CachedUserAccessProvider()
        {
            using var scope = fixture.Services.CreateScope();
            var provider = scope.ServiceProvider.GetService<IUserAccessProvider>();

            Assert.NotNull(provider);
            Assert.IsType<CachedUserAccessProvider>(provider);
        }

        [Fact]
        public void At_least_one_IReportProvider_is_registered()
        {
            using var scope = fixture.Services.CreateScope();
            var providers = scope.ServiceProvider.GetServices<IReportProvider>();

            Assert.NotEmpty(providers);
        }

        [Fact]
        public void At_least_one_IReportExporter_is_registered()
        {
            using var scope = fixture.Services.CreateScope();
            var exporters = scope.ServiceProvider.GetServices<IReportExporter>();

            Assert.NotEmpty(exporters);
        }

        [Fact]
        public void At_least_one_IDashboardWidget_is_registered()
        {
            using var scope = fixture.Services.CreateScope();
            var widgets = scope.ServiceProvider.GetServices<IDashboardWidget>();

            Assert.NotEmpty(widgets);
        }
    }
}

