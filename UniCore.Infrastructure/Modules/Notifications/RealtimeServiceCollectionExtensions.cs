using Microsoft.Extensions.DependencyInjection;
using UniCore.Application.Modules.Notifications;

namespace UniCore.Infrastructure.Modules.Notifications
{
    public static class RealtimeServiceCollectionExtensions
    {
        public static IServiceCollection AddRealtime(this IServiceCollection services)
        {
            services.AddSignalR();
            services.AddSingleton<INotificationPushSender, SignalRNotificationPushSender>();

            return services;
        }
    }

}
