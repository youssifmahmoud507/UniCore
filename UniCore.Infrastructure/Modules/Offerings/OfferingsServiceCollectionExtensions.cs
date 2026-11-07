using Microsoft.Extensions.DependencyInjection;
using UniCore.Application.Modules.Offerings;
using UniCore.Application.Modules.People;

namespace UniCore.Infrastructure.Modules.Offerings
{
    // Everything the Offerings module needs in the container. Called once from AddInfrastructure.
    public static class OfferingsServiceCollectionExtensions
    {
        public static IServiceCollection AddOfferingsModule(this IServiceCollection services)
        {
            services.AddScoped<ISectionEnrollmentCounter, SectionSeatCounter>();
            services.AddScoped<IOfferingReadService, OfferingReadService>();

            // The real reader of People's scope data (it replaced the empty placeholder registered by AddPeopleModule).
            services.AddScoped<ISectionAssignmentReader, SectionAssignmentReader>();

            services.AddScoped<OfferingAccess>();
            services.AddScoped<CourseOfferingHandler>();
            services.AddScoped<CourseSectionHandler>();
            services.AddScoped<InstructorAssignmentHandler>();

            return services;
        }
    }
}
