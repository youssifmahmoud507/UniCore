using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using UniCore.Api;
using UniCore.Application.Common.Abstractions;
using UniCore.Application.Modules.Finance;
using UniCore.Application.Modules.Students;
using UniCore.Infrastructure.Modules.Finance;
using UniCore.Infrastructure.Modules.Students;

namespace UniCore.IntegrationTests.Infrastructure
{
    public sealed class UniCoreApiFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");

            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IEmailSender>();
                services.AddSingleton<CapturingEmailSender>();
                services.AddSingleton<IEmailSender>(sp => sp.GetRequiredService<CapturingEmailSender>());
                // These tests are about students and finance, not programs: accept any program id.
                // The real verifier is covered by the University tests.
                services.RemoveAll<IAcademicPlacementVerifier>();
                services.AddScoped<IAcademicPlacementVerifier, UnverifiedAcademicPlacementVerifier>();
                // Same for the finance references: these tests use made-up university and program ids.
                // FinanceReferenceVerifierTests covers the real verifier.
                services.RemoveAll<IFinanceReferenceVerifier>();
                services.AddScoped<IFinanceReferenceVerifier, UnverifiedFinanceReferenceVerifier>();
            });
        }
    }

}
