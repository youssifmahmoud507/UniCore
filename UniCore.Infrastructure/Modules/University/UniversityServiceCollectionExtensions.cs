using Microsoft.Extensions.DependencyInjection;
using UniCore.Application.Modules.Identity;
using UniCore.Application.Modules.Students;
using UniCore.Application.Modules.University;

namespace UniCore.Infrastructure.Modules.University
{
    // Everything the University module needs in the container. Called once from AddInfrastructure.
    public static class UniversityServiceCollectionExtensions
    {
        public static IServiceCollection AddUniversityModule(this IServiceCollection services)
        {
            // Real hierarchy and placement checks for the partner's contracts. They read the database,
            // so they are scoped (one per request), not singletons.
            services.AddScoped<IScopeHierarchy, UniversityScopeHierarchy>();
            services.AddScoped<IAcademicPlacementVerifier, AcademicPlacementVerifier>();
            services.AddScoped<IProgramUsageChecker, ProgramUsageChecker>();

            services.AddScoped<UniversityManagementHandler>();
            services.AddScoped<CampusManagementHandler>();
            services.AddScoped<FacultyManagementHandler>();
            services.AddScoped<DepartmentManagementHandler>();
            services.AddScoped<ProgramManagementHandler>();

            return services;
        }
    }
}
