using System.Text;
using UniCore.Application.Modules.Reporting;

namespace UniCore.Infrastructure.Modules.Reporting.Export
{
    // RFC 4180 CSV for spreadsheets: UTF-8 with a byte order mark (Excel then reads other alphabets correctly),
    // CRLF line ends, header row first.
    //
    // CSV injection: a text that starts with = + - @ (or a tab or a carriage return) is run as a formula by Excel
    // when the file is opened. Such text gets a leading apostrophe, which Excel shows as plain text.
    public sealed class CsvReportExporter : IReportExporter
    {
        private static readonly char[] FormulaStarts = ['=', '+', '-', '@', '\t', '\r'];

        public ReportFormat Format => ReportFormat.Csv;

        public string ContentType => "text/csv; charset=utf-8";

        public string Extension => "csv";

        public int MaxRows => 100_000;

        public byte[] Export(ReportExportModel model)
        {
            var text = new StringBuilder();

            text.Append(string.Join(',', model.Table.Columns.Select(c => Escape(c.Label)))).Append("\r\n");

            foreach (var row in model.Table.Rows)
            {
                text.Append(string.Join(',', row.Select(Cell))).Append("\r\n");
            }

            return [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes(text.ToString())];
        }

        private static string Cell(object? value)
        {
            var text = ReportCells.Text(value);

            // Only real text can be a formula: a negative number is a number, not text.
            return value is string && text.Length > 0 && Array.IndexOf(FormulaStarts, text[0]) >= 0 ? Escape("'" + text) : Escape(text);
        }

        private static string Escape(string text) => text.IndexOfAny(['"', ',', '\r', '\n']) >= 0 ? "\"" + text.Replace("\"", "\"\"") + "\"" : text;
    }




}
