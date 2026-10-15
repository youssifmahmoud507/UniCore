using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using UniCore.Api;
using UniCore.IntegrationTests.Infrastructure;

namespace UniCore.IntegrationTests.Api
{
    [Collection(ApiCollection.Name)]
    public sealed class ServiceRegistrationTests(ApiFixture fixture)
    {
        // Every handler and service a controller asks for (constructor or [FromServices]) must be registered,
        // otherwise that endpoint fails with a 500 at runtime.
        [Fact]
        public void Every_service_a_controller_needs_can_be_resolved()
        {
            using var scope = fixture.Services.CreateScope();
            var missing = new List<string>();

            var controllers = typeof(Program).Assembly.GetTypes()
                .Where(t => t is { IsClass: true, IsAbstract: false } && typeof(ControllerBase).IsAssignableFrom(t));

            foreach (var controller in controllers)
            {
                var constructorParameters = controller.GetConstructors().SelectMany(c => c.GetParameters());

                var actionParameters = controller.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                    .SelectMany(m => m.GetParameters())
                    .Where(p => p.GetCustomAttributes<FromServicesAttribute>().Any());

                foreach (var parameter in constructorParameters.Concat(actionParameters))
                {
                    if (scope.ServiceProvider.GetService(parameter.ParameterType) is null)
                    {
                        missing.Add($"{controller.Name} needs {parameter.ParameterType.Name}");
                    }
                }
            }

            Assert.Empty(missing);
        }
    }
}