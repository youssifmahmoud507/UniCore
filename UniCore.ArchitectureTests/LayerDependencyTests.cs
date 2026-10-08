using Microsoft.CodeAnalysis;
using System.Reflection;

namespace UniCore.ArchitectureTests
{
    public class LayerDependencyTests
    {
        private static readonly Assembly Domain = typeof(UniCore.Domain.Common.Results.Result).Assembly;
        private static readonly Assembly Application = typeof(UniCore.Application.Common.Abstractions.IClock).Assembly;
        private static readonly Assembly Infrastructure = typeof(UniCore.Infrastructure.Common.SystemClock).Assembly;
        private static readonly Assembly Api = typeof(UniCore.Api.Program).Assembly;

        private static IReadOnlyList<string> Violations(Assembly assembly, params string[] forbiddenPrefixes) =>
            [.. assembly.GetReferencedAssemblies()
                .Select(r => r.Name ?? string.Empty)
                .Where(name => forbiddenPrefixes.Any(p => name.StartsWith(p, StringComparison.Ordinal)))];

        [Fact]
        public void Domain_depends_on_nothing_of_ours_and_no_frameworks()
        {
            var violations = Violations(Domain,
                "UniCore.Application", "UniCore.Infrastructure", "UniCore.Api",
                "Microsoft.EntityFrameworkCore", "Microsoft.AspNetCore");

            Assert.True(violations.Count == 0, "Domain must not reference: " + string.Join(", ", violations));
        }

        [Fact]
        public void Application_depends_only_on_Domain()
        {
            var violations = Violations(Application,
                "UniCore.Infrastructure", "UniCore.Api", "Microsoft.EntityFrameworkCore");

            Assert.True(violations.Count == 0, "Application must not reference: " + string.Join(", ", violations));
        }

        [Fact]
        public void Infrastructure_does_not_depend_on_Api()
        {
            var violations = Violations(Infrastructure, "UniCore.Api");

            Assert.True(violations.Count == 0, "Infrastructure must not reference: " + string.Join(", ", violations));
        }

        [Fact]
        public void Api_assembly_is_loadable()
        {
            Assert.NotNull(Api);
        }
    }
}
