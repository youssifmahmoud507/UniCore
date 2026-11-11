namespace UniCore.Application.Modules.Reporting
{
    public interface IReportExporter
    {
        ReportFormat Format { get; }

        string ContentType { get; }

        string Extension { get; }

        // More rows than this is refused for this format (a PDF is for reading, not for a whole table).
        int MaxRows { get; }

        byte[] Export(ReportExportModel model);
    }




}
