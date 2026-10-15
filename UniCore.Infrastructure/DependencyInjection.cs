using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using UniCore.Application.Common.Abstractions;
using UniCore.Application.Common.Repositories;
using UniCore.Application.Modules.Identity;
using UniCore.Application.Modules.Students;
using UniCore.Domain.Common.Results;
using UniCore.Infrastructure.Common;
using UniCore.Infrastructure.Modules.Audit;
using UniCore.Infrastructure.Modules.Identity;
using UniCore.Infrastructure.Modules.Platform;
using UniCore.Infrastructure.Modules.Students;
using UniCore.Infrastructure.Modules.University;
using UniCore.Infrastructure.Persistence;
using UniCore.Infrastructure.Persistence.Repositories;

namespace UniCore.Infrastructure
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddInfrastructure(this IServiceCollection services, string connectionString)
        {
            services.AddDbContext<AppDbContext>(options => options.UseSqlServer(connectionString));

            services.AddSingleton<IClock, SystemClock>();
            services.AddScoped<IUnitOfWork, UnitOfWork>();
            services.AddHttpContextAccessor();
            services.AddScoped<ICurrentUser, CurrentUser>();
            services.AddScoped(typeof(IRepository<>), typeof(GenericRepository<>));

            services.AddDataProtection();


            // Identity core only: JWT authentication is added in the next batch.
            services.AddIdentityCore<ApplicationUser>(IdentityOptionsSetup.Configure)
                .AddRoles<ApplicationRole>()
                .AddEntityFrameworkStores<AppDbContext>()
                .AddDefaultTokenProviders();

            services.AddScoped<IdentitySeeder>();



            services.AddScoped<AuthorizationSeeder>();
            services.AddScoped<IUserAccessContext, UserAccessContext>();
            services.AddScoped<IUserAccessProvider, UserAccessProvider>();
            // IScopeHierarchy and IAcademicPlacementVerifier are registered by AddUniversityModule below.
            services.AddScoped<IPermissionAuthorizer, PermissionAuthorizer>();
            services.AddScoped<IUserAccountService, UserAccountService>();
            services.AddSingleton<IAuditWriter, DbAuditWriter>();
            services.AddScoped<IIdentityAdministration, IdentityAdministration>();
            services.AddScoped<RolePermissionCatalog>();
            services.AddScoped<RoleManagementHandler>();
            services.AddScoped<UserManagementHandler>();
            services.AddScoped<IStudentNumberGenerator, StudentNumberGenerator>();
            services.AddUniversityModule();
            services.AddScoped<IStudentAccountProvisioner, StudentAccountProvisioner>();
            services.AddScoped<StudentAdmissionService>();
            services.AddScoped<StudentManagementHandler>();
            services.AddScoped(typeof(IAggregateRepository<>), typeof(AggregateRepository<>));

            // Use-case handlers (Application layer), registered here because this is the composition root.
            services.AddScoped<LoginHandler>();
            services.AddScoped<RefreshHandler>();
            services.AddScoped<LogoutHandler>();

            services.AddHealthChecks().AddDbContextCheck<AppDbContext>("database", tags: ["ready"]);

            services.AddPlatformModules(connectionString);


            return services;
        }

        public static async Task<Result> SeedIdentityAsync(this IServiceProvider services)
        {
            using var scope = services.CreateScope();

            var identity = scope.ServiceProvider.GetService<IdentitySeeder>();
            var authorization = scope.ServiceProvider.GetService<AuthorizationSeeder>();

            if (identity is null || authorization is null)
            {
                return Result.Fail(Error.Failure("SEEDER_MISSING", "A seeder is not registered."));
            }

            // Roles first: the default role permissions need them.
            var roles = await identity.SeedAsync();
            if (roles.IsFailure)
            {
                return roles;
            }

            return await authorization.SeedAsync();
        }
    }
}
