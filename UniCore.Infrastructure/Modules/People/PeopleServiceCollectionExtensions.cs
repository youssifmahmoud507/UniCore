using Microsoft.Extensions.DependencyInjection;
using UniCore.Application.Modules.People;

namespace UniCore.Infrastructure.Modules.People
{
    public static class PeopleServiceCollectionExtensions
    {
        public static IServiceCollection AddPeopleModule(this IServiceCollection services)
        {
            services.AddScoped<ISectionAssignmentReader, NoSectionAssignments>();
            services.AddScoped<IScopeDataProvider, ScopeDataProvider>();
            services.AddScoped<PersonProfileHandler>();
            services.AddScoped<EmployeeHandler>();
            services.AddScoped<InstructorHandler>();
            services.AddScoped<TeachingAssistantHandler>();
            return services;
        }
    }
}
