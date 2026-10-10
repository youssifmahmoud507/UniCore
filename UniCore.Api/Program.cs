
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Scalar.AspNetCore;
using Serilog;
using Serilog.Events;
using System.Text.Json.Serialization;
using UniCore.Api.Extensions;
using UniCore.Api.Middleware;
using UniCore.Application.Common.Abstractions;
using UniCore.Infrastructure;
using UniCore.Infrastructure.Common;
using UniCore.Infrastructure.Modules.Email;
using UniCore.Infrastructure.Modules.Identity;


namespace UniCore.Api
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            builder.Host.UseSerilog((context, services, logger) => logger
                .MinimumLevel.Information()
                .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
                .MinimumLevel.Override("Microsoft.EntityFrameworkCore.Database.Command", LogEventLevel.Warning)
                .Enrich.FromLogContext()
                .WriteTo.Console(outputTemplate:
                    "[{Timestamp:HH:mm:ss} {Level:u3}] [{CorrelationId}] {Message:lj}{NewLine}{Exception}"));

            builder.Services.AddControllers().AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

            builder.Services.AddOpenApi();


            var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? string.Empty;
            builder.Services.AddInfrastructure(connectionString);
            builder.Services.AddJwtAuthentication(builder.Configuration);
            builder.Services.AddEmailSending(builder.Configuration);
            builder.Services.AddPasswordReset(builder.Configuration);
            builder.Services.AddApiRateLimiting();

            var app = builder.Build();

            app.UseMiddleware<CorrelationIdMiddleware>();
            app.UseSerilogRequestLogging();
            app.UseMiddleware<GlobalExceptionMiddleware>();

            if (app.Environment.IsDevelopment())
            {
                app.MapOpenApi();
                app.MapScalarApiReference();
            }

            app.UseHttpsRedirection();
            app.UseRateLimiter();
            app.UseAuthentication();
            app.UseAuthorization();

            app.MapControllers();

            app.MapHealthChecks("/health/live", new HealthCheckOptions
            {
                Predicate = _ => false
            });
            app.MapHealthChecks("/health/ready", new HealthCheckOptions
            {
                Predicate = check => check.Tags.Contains("ready")
            });
            await app.Services.SeedIdentityAsync();

            app.Run();
        }
    }
}
