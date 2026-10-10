using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Text;
using UniCore.Application.Common.Abstractions;
using UniCore.Application.Common.Repositories;
using UniCore.Infrastructure.Common;
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
            services.AddScoped(typeof(IRepository<>), typeof(GenericRepository<>));
            services.AddHealthChecks().AddDbContextCheck<AppDbContext>("database", tags: ["ready"]);


            return services;
        }
    }
}
