using UniCore.Application.Modules.Reporting;

namespace UniCore.UnitTests.Modules.Reporting
{
    internal sealed class StubExporter(ReportFormat format, int maxRows = 1000) : IReportExporter
    {
        public ReportFormat Format { get; } = format;
        public string ContentType => $"application/x-{Format.ToString().ToLowerInvariant()}";
        public string Extension => Format.ToString().ToLowerInvariant();
        public int MaxRows { get; } = maxRows;
        public ReportExportModel? LastModel { get; private set; }

        public byte[] Export(ReportExportModel model)
        {
            LastModel = model;
            return [1, 2, 3];
        }
    }




}
