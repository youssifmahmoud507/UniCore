namespace UniCore.Application.Modules.Reporting
{
    // Every report that was registered. Other modules add theirs by registering an IReportProvider.
    public sealed class ReportCatalog(IEnumerable<IReportProvider> providers)
    {
        private readonly IReadOnlyList<IReportProvider> _providers = [.. providers.OrderBy(p => p.Descriptor.Category, StringComparer.Ordinal).ThenBy(p => p.Descriptor.Code, StringComparer.Ordinal)];

        public IReadOnlyList<IReportProvider> All => _providers;

        public IReportProvider? Find(string? code)
            => _providers.FirstOrDefault(p => string.Equals(p.Descriptor.Code, code?.Trim(), StringComparison.OrdinalIgnoreCase));
    }




}
