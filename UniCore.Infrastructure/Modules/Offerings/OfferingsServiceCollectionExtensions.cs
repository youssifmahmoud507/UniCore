using Microsoft.Extensions.DependencyInjection;
using UniCore.Application.Modules.Offerings;

namespace UniCore.Infrastructure.Modules.Offerings
{
    // Everything the Offerings module needs in the container. Called once from AddInfrastructure.
    public static class OfferingsServiceCollectionExtensions
    {
        public static IServiceCollection AddOfferingsModule(this IServiceCollection services)
        {
            services.AddScoped<ISectionEnrollmentCounter, NoEnrollmentsYetCounter>();
            services.AddScoped<IOfferingReadService, OfferingReadService>();

            services.AddScoped<OfferingAccess>();
            services.AddScoped<CourseOfferingHandler>();
            services.AddScoped<CourseSectionHandler>();
            services.AddScoped<InstructorAssignmentHandler>();

            return services;
        }
    }
}
