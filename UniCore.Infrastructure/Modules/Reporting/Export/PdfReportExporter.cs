using System.Globalization;
using System.Text;
using UniCore.Application.Modules.Reporting;

namespace UniCore.Infrastructure.Modules.Reporting.Export
{
    // A PDF table written directly (PDF 1.4, the built-in Helvetica font), so no library (and no licence) is needed.
    // A4 landscape, the header row repeats on every page, "Page x of y" at the bottom.
    //
    // LIMIT: the built-in fonts hold Western European letters only. Any other character (Arabic, for example) is
    // printed as "?". For such data use the Excel or CSV export, which keep every character. A drop-in exporter with
    // an embedded font can replace this class later without touching anything else.
    //
    // A PDF is for reading: it holds up to MaxRows rows.
    public sealed class PdfReportExporter : IReportExporter
    {
        private const double PageWidth = 842;
        private const double PageHeight = 595;
        private const double Margin = 36;
        private const double RowHeight = 14;
        private const double FontSize = 8;
        private const double CellPadding = 4;
        private const double MinColumnWidth = 36;
        private const double MaxColumnWidth = 230;
        private const double TextFloor = 70;
        private const double TitleBlockHeight = 62;

        public ReportFormat Format => ReportFormat.Pdf;

        public string ContentType => "application/pdf";

        public string Extension => "pdf";

        public int MaxRows => 2_000;

        public byte[] Export(ReportExportModel model)
        {
            var columns = model.Table.Columns;
            var widths = ColumnWidths(model.Table);
            var cells = model.Table.Rows.Select(r => columns.Select((c, i) => Fit(ReportCells.Text(i < r.Count ? r[i] : null), widths[i])).ToArray()).ToList();
            var pages = Paginate(cells.Count);
            var content = new List<string>();

            for (var p = 0; p < pages.Count; p++)
            {
                content.Add(RenderPage(model, columns, widths, cells, pages[p], p == 0, p + 1, pages.Count));
            }

            return Assemble(model, content);
        }

        // Rows per page: the first page has the title block, so it holds fewer.
        private static List<(int Start, int Count)> Paginate(int rowCount)
        {
            var usable = PageHeight - 2 * Margin - 16;
            var first = Math.Max(1, (int)((usable - TitleBlockHeight - RowHeight) / RowHeight));
            var other = Math.Max(1, (int)((usable - RowHeight) / RowHeight));
            var pages = new List<(int, int)>();
            var start = 0;

            do
            {
                var take = Math.Min(pages.Count == 0 ? first : other, rowCount - start);
                pages.Add((start, take));
                start += take;
            }
            while (start < rowCount);

            return pages;
        }

        private static string RenderPage(ReportExportModel model, IReadOnlyList<ReportColumn> columns, double[] widths, List<string[]> cells, (int Start, int Count) page, bool first, int number, int total)
        {
            var draw = new StringBuilder();
            var y = PageHeight - Margin;

            if (first)
            {
                Text(draw, "F2", 14, Margin, y - 12, model.Title);
                Text(draw, "F1", 9, Margin, y - 27, model.Description);
                Text(draw, "F1", 8, Margin, y - 41, "Filters: " + model.Parameters);
                Text(draw, "F1", 8, Margin, y - 53, "Generated (UTC): " + ReportCells.Text(model.GeneratedAt) + "   Rows: " + model.Table.Rows.Count.ToString(CultureInfo.InvariantCulture));
                y -= TitleBlockHeight;
            }

            // Header row on a grey band.
            draw.Append(CultureInfo.InvariantCulture, $"0.90 0.90 0.90 rg {Margin:0.##} {y - RowHeight:0.##} {widths.Sum():0.##} {RowHeight:0.##} re f 0 g\n");
            var x = Margin;

            for (var c = 0; c < columns.Count; c++)
            {
                Text(draw, "F2", FontSize, x + CellPadding, y - RowHeight + 4, Fit(columns[c].Label, widths[c]));
                x += widths[c];
            }

            y -= RowHeight;

            for (var r = page.Start; r < page.Start + page.Count; r++)
            {
                x = Margin;

                for (var c = 0; c < columns.Count; c++)
                {
                    var value = cells[r][c];
                    var right = columns[c].Type is ReportColumnType.Integer or ReportColumnType.Decimal;
                    var position = right ? x + widths[c] - CellPadding - Measure(value) : x + CellPadding;

                    Text(draw, "F1", FontSize, position, y - RowHeight + 4, value);
                    x += widths[c];
                }

                y -= RowHeight;
                draw.Append(CultureInfo.InvariantCulture, $"0.85 G 0.4 w {Margin:0.##} {y:0.##} m {Margin + widths.Sum():0.##} {y:0.##} l S 0 G\n");
            }

            Text(draw, "F1", 8, Margin, Margin - 14, "UniCore");
            var footer = $"Page {number} of {total}";
            Text(draw, "F1", 8, PageWidth - Margin - Measure(footer), Margin - 14, footer);

            return draw.ToString();
        }

        private static byte[] Assemble(ReportExportModel model, List<string> pageContent)
        {
            var objects = new List<string>();
            var pageNumbers = new List<int>();

            // 1 catalog, 2 page tree, 3 and 4 fonts, 5 info; then a (page, content) pair per page.
            objects.Add("<< /Type /Catalog /Pages 2 0 R >>");
            objects.Add(string.Empty);
            objects.Add("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>");
            objects.Add("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold /Encoding /WinAnsiEncoding >>");
            objects.Add($"<< /Title ({Escape(Latin(model.Title))}) /Producer (UniCore) /CreationDate (D:{model.GeneratedAt.UtcDateTime:yyyyMMddHHmmss}Z) >>");

            foreach (var content in pageContent)
            {
                var pageNumber = objects.Count + 1;
                var contentNumber = pageNumber + 1;
                pageNumbers.Add(pageNumber);

                objects.Add($"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {PageWidth:0} {PageHeight:0}] /Resources << /Font << /F1 3 0 R /F2 4 0 R >> >> /Contents {contentNumber} 0 R >>");
                objects.Add($"<< /Length {Encoding.Latin1.GetByteCount(content)} >>\nstream\n{content}\nendstream");
            }

            objects[1] = $"<< /Type /Pages /Kids [{string.Join(' ', pageNumbers.Select(n => $"{n} 0 R"))}] /Count {pageNumbers.Count} >>";

            using var file = new MemoryStream();

            void Write(string text)
            {
                var bytes = Encoding.Latin1.GetBytes(text);
                file.Write(bytes, 0, bytes.Length);
            }

            Write("%PDF-1.4\n%\u00e2\u00e3\u00cf\u00d3\n");

            var offsets = new List<long>();

            for (var i = 0; i < objects.Count; i++)
            {
                offsets.Add(file.Position);
                Write($"{i + 1} 0 obj\n{objects[i]}\nendobj\n");
            }

            var xref = file.Position;
            Write($"xref\n0 {objects.Count + 1}\n0000000000 65535 f \n");

            foreach (var offset in offsets)
            {
                Write($"{offset:0000000000} 00000 n \n");
            }

            Write($"trailer\n<< /Size {objects.Count + 1} /Root 1 0 R /Info 5 0 R >>\nstartxref\n{xref}\n%%EOF\n");

            return file.ToArray();
        }

        private static double[] ColumnWidths(ReportTable table)
        {
            var widths = new double[table.Columns.Count];

            for (var c = 0; c < widths.Length; c++)
            {
                var longest = Measure(table.Columns[c].Label);

                foreach (var row in table.Rows.Take(300))
                {
                    longest = Math.Max(longest, Measure(ReportCells.Text(c < row.Count ? row[c] : null)));
                }

                widths[c] = Math.Clamp(longest + 2 * CellPadding, MinColumnWidth, MaxColumnWidth);
            }

            Shrink(widths, table.Columns);

            return widths;
        }

        // Too wide for the page: take the width from the text columns (which can be cut) and keep the numbers whole.
        private static void Shrink(double[] widths, IReadOnlyList<ReportColumn> columns)
        {
            var usable = PageWidth - 2 * Margin;
            var excess = widths.Sum() - usable;

            if (excess <= 0 || widths.Length == 0)
            {
                return;
            }

            var spare = widths.Select((w, c) => columns[c].Type is ReportColumnType.Integer or ReportColumnType.Decimal ? 0d : Math.Max(0d, w - TextFloor)).ToArray();
            var available = spare.Sum();

            if (available >= excess)
            {
                for (var c = 0; c < widths.Length; c++)
                {
                    widths[c] -= excess * spare[c] / available;
                }

                return;
            }

            // Not enough room even then: scale everything down evenly.
            var factor = usable / widths.Sum();

            for (var c = 0; c < widths.Length; c++)
            {
                widths[c] = Math.Max(24, widths[c] * factor);
            }
        }

        // The text cut so it fits the column, ending with "..." when something was left out.
        private static string Fit(string text, double width)
        {
            var clean = Latin(text);
            var room = width - 2 * CellPadding;

            // The small tolerance absorbs floating point rounding of the measured width.
            if (Measure(clean) <= room + 0.01)
            {
                return clean;
            }

            var cut = clean;

            while (cut.Length > 0 && Measure(cut + "...") > room + 0.01)
            {
                cut = cut[..^1];
            }

            return cut + "...";
        }

        // Helvetica widths in 1/1000 em, close enough for digits, punctuation and letters to align and cut text.
        private static double Measure(string text)
        {
            double units = 0;

            foreach (var ch in text)
            {
                units += ch switch
                {
                    ' ' or '.' or ',' or ':' or ';' or '!' or '|' or 'i' or 'j' or 'l' or '\'' => 278,
                    '(' or ')' or '-' or 'f' or 't' or 'I' or '[' or ']' or '/' => 333,
                    'm' or 'M' or '%' => 833,
                    'W' or 'w' => 722,
                    >= '0' and <= '9' => 556,
                    >= 'A' and <= 'Z' => 667,
                    _ => 556
                };
            }

            return units * FontSize / 1000d;
        }

        private static void Text(StringBuilder draw, string font, double size, double x, double y, string text)
        {
            if (text.Length == 0)
            {
                return;
            }

            draw.Append(CultureInfo.InvariantCulture, $"BT /{font} {size:0.##} Tf {x:0.##} {y:0.##} Td ({Escape(Latin(text))}) Tj ET\n");
        }

        // Only what the built-in font can show: ASCII and Latin-1. Everything else becomes "?".
        private static string Latin(string text)
        {
            var clean = new StringBuilder(text.Length);

            foreach (var ch in text)
            {
                clean.Append(ch switch
                {
                    '\u2013' or '\u2014' => '-',
                    '\u2018' or '\u2019' => '\'',
                    '\u201c' or '\u201d' => '"',
                    '\r' or '\n' or '\t' => ' ',
                    < ' ' => ' ',
                    > '\u007e' and < '\u00a0' => '?',
                    > '\u00ff' => '?',
                    _ => ch
                });
            }

            return clean.ToString();
        }

        // PDF string syntax: \ ( ) are escaped, and every byte above 126 is written as an octal escape.
        private static string Escape(string text)
        {
            var escaped = new StringBuilder(text.Length);

            foreach (var ch in text)
            {
                if (ch is '\\' or '(' or ')')
                {
                    escaped.Append('\\').Append(ch);
                }
                else if (ch > '\u007e')
                {
                    escaped.Append('\\').Append(Convert.ToString(ch, 8).PadLeft(3, '0'));
                }
                else
                {
                    escaped.Append(ch);
                }
            }

            return escaped.ToString();
        }
    }




}
