using Microsoft.Extensions.DependencyInjection;
using UniCore.Application.Modules.Transcripts;

namespace UniCore.Infrastructure.Modules.Transcripts
{
    // Everything the Transcripts module needs in the container. Called once from AddInfrastructure.
    public static class TranscriptsServiceCollectionExtensions
    {
        public static IServiceCollection AddTranscriptsModule(this IServiceCollection services)
        {
            services.AddScoped<TranscriptAccess>();
            services.AddScoped<AcademicHistoryBuilder>();
            services.AddScoped<IGpaCalculationService, GpaCalculationService>();
            services.AddScoped<ITranscriptSnapshotService, TranscriptSnapshotService>();
            services.AddScoped<TranscriptQueryHandler>();
            services.AddScoped<TranscriptIssueHandler>();

            return services;
        }
    }
}
