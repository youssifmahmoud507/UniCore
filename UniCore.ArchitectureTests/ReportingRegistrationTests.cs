using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UniCore.Application.Modules.Reporting;
using UniCore.Application.Modules.Reporting.Dashboard;
using UniCore.Infrastructure.Modules.Reporting;

namespace UniCore.ArchitectureTests
{
    // A report or a dashboard widget that was written but never registered would silently never show up.
    public class ReportingRegistrationTests
    {
        private static readonly Assembly[] Assemblies =
        [
            typeof(ReportCatalog).Assembly,
            typeof(ReportingServiceCollectionExtensions).Assembly
        ];

        private static HashSet<Type> Implementations<TContract>()
            => Assemblies.SelectMany(a => a.GetTypes())
                .Where(t => t is { IsClass: true, IsAbstract: false } && typeof(TContract).IsAssignableFrom(t))
                .ToHashSet();

        private static HashSet<Type?> Registered<TContract>()
            => new ServiceCollection().AddReportingModule()
                .Where(d => d.ServiceType == typeof(TContract))
                .Select(d => d.ImplementationType)
                .ToHashSet();

        [Fact]
        public void Every_report_is_registered()
        {
            var defined = Implementations<IReportProvider>();
            var missing = defined.Where(t => !Registered<IReportProvider>().Contains(t)).Select(t => t.Name).ToList();

            Assert.NotEmpty(defined);
            Assert.True(missing.Count == 0, "Register in AddReportingModule: " + string.Join(", ", missing));
        }

        [Fact]
        public void Every_dashboard_widget_is_registered()
        {
            var defined = Implementations<IDashboardWidget>();
            var missing = defined.Where(t => !Registered<IDashboardWidget>().Contains(t)).Select(t => t.Name).ToList();

            Assert.NotEmpty(defined);
            Assert.True(missing.Count == 0, "Register in AddReportingModule: " + string.Join(", ", missing));
        }

        [Fact]
        public void Every_export_format_is_registered_once()
        {
            var formats = new ServiceCollection().AddReportingModule()
                .Where(d => d.ServiceType == typeof(IReportExporter))
                .Select(d => d.ImplementationType)
                .ToList();

            Assert.Equal(Implementations<IReportExporter>().Count, formats.Count);
            Assert.Equal(formats.Count, formats.Distinct().Count());
        }
    }
}
