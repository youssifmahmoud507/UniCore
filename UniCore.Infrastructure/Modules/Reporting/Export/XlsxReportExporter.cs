using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Xml;
using UniCore.Application.Modules.Reporting;

namespace UniCore.Infrastructure.Modules.Reporting.Export
{
    // An Excel workbook written directly as Office Open XML, so no library (and no licence) is needed.
    //   Sheet "Report": the table, header row frozen and filterable, real numbers and dates.
    //   Sheet "Info": title, description, filters and when it was made.
    //
    // Text cells are stored as plain strings, never as formulas, so a value such as =1+1 stays text in Excel.
    public sealed class XlsxReportExporter : IReportExporter
    {
        private const int MaxCellText = 32_000;
        private const int MinWidth = 8;
        private const int MaxWidth = 60;
        private static readonly DateOnly ExcelEpoch = new(1899, 12, 30);

        // Styles in styles.xml (cellXfs index).
        private const int Default = 0;
        private const int Header = 1;
        private const int Integer = 2;
        private const int Decimal = 3;
        private const int DateStyle = 4;
        private const int DateTimeStyle = 5;
        private const int Bold = 6;

        public ReportFormat Format => ReportFormat.Xlsx;

        public string ContentType => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

        public string Extension => "xlsx";

        public int MaxRows => 50_000;

        public byte[] Export(ReportExportModel model)
        {
            using var stream = new MemoryStream();

            using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
            {
                Add(zip, "[Content_Types].xml", ContentTypes);
                Add(zip, "_rels/.rels", RootRelationships);
                Add(zip, "xl/workbook.xml", Workbook);
                Add(zip, "xl/_rels/workbook.xml.rels", WorkbookRelationships);
                Add(zip, "xl/styles.xml", Styles);
                Add(zip, "xl/worksheets/sheet1.xml", ReportSheet(model.Table));
                Add(zip, "xl/worksheets/sheet2.xml", InfoSheet(model));
            }

            return stream.ToArray();
        }

        private static string ReportSheet(ReportTable table)
        {
            var xml = new StringBuilder();
            var columnCount = Math.Max(1, table.Columns.Count);
            var lastColumn = ColumnName(columnCount - 1);

            xml.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
            xml.Append("<worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">");
            xml.Append("<sheetViews><sheetView workbookViewId=\"0\"><pane ySplit=\"1\" topLeftCell=\"A2\" activePane=\"bottomLeft\" state=\"frozen\"/></sheetView></sheetViews>");
            xml.Append("<cols>");

            for (var c = 0; c < table.Columns.Count; c++)
            {
                var longest = Math.Max(table.Columns[c].Label.Length, table.Rows.Take(200).Select(r => ReportCells.Text(c < r.Count ? r[c] : null).Length).DefaultIfEmpty(0).Max());
                var width = Math.Clamp(longest + 2, MinWidth, MaxWidth);

                xml.Append(CultureInfo.InvariantCulture, $"<col min=\"{c + 1}\" max=\"{c + 1}\" width=\"{width}\" customWidth=\"1\"/>");
            }

            xml.Append("</cols><sheetData>");

            xml.Append("<row r=\"1\">");

            for (var c = 0; c < table.Columns.Count; c++)
            {
                AppendText(xml, ColumnName(c) + "1", table.Columns[c].Label, Header);
            }

            xml.Append("</row>");

            for (var r = 0; r < table.Rows.Count; r++)
            {
                var number = r + 2;
                xml.Append(CultureInfo.InvariantCulture, $"<row r=\"{number}\">");

                for (var c = 0; c < table.Columns.Count; c++)
                {
                    AppendValue(xml, ColumnName(c) + number.ToString(CultureInfo.InvariantCulture), c < table.Rows[r].Count ? table.Rows[r][c] : null);
                }

                xml.Append("</row>");
            }

            xml.Append("</sheetData>");

            if (table.Columns.Count > 0)
            {
                xml.Append(CultureInfo.InvariantCulture, $"<autoFilter ref=\"A1:{lastColumn}{Math.Max(1, table.Rows.Count + 1)}\"/>");
            }

            xml.Append("</worksheet>");

            return xml.ToString();
        }

        private static string InfoSheet(ReportExportModel model)
        {
            var xml = new StringBuilder();

            xml.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
            xml.Append("<worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\"><cols><col min=\"1\" max=\"1\" width=\"18\" customWidth=\"1\"/><col min=\"2\" max=\"2\" width=\"90\" customWidth=\"1\"/></cols><sheetData>");

            var lines = new (string Label, string Value)[]
            {
                ("Report", model.Title),
                ("Description", model.Description),
                ("Filters", model.Parameters),
                ("Generated (UTC)", ReportCells.Text(model.GeneratedAt)),
                ("Rows", model.Table.Rows.Count.ToString(CultureInfo.InvariantCulture))
            };

            for (var i = 0; i < lines.Length; i++)
            {
                var row = i + 1;
                xml.Append(CultureInfo.InvariantCulture, $"<row r=\"{row}\">");
                AppendText(xml, "A" + row.ToString(CultureInfo.InvariantCulture), lines[i].Label, Bold);
                AppendText(xml, "B" + row.ToString(CultureInfo.InvariantCulture), lines[i].Value, Default);
                xml.Append("</row>");
            }

            xml.Append("</sheetData></worksheet>");

            return xml.ToString();
        }

        private static void AppendValue(StringBuilder xml, string reference, object? value)
        {
            switch (value)
            {
                case null:
                    return;

                case string text:
                    AppendText(xml, reference, text, Default);
                    return;

                case bool flag:
                    xml.Append(CultureInfo.InvariantCulture, $"<c r=\"{reference}\" t=\"b\"><v>{(flag ? 1 : 0)}</v></c>");
                    return;

                case int or long or short or byte:
                    xml.Append(CultureInfo.InvariantCulture, $"<c r=\"{reference}\" s=\"{Integer}\"><v>{Convert.ToString(value, CultureInfo.InvariantCulture)}</v></c>");
                    return;

                case decimal number:
                    xml.Append(CultureInfo.InvariantCulture, $"<c r=\"{reference}\" s=\"{Decimal}\"><v>{number.ToString(CultureInfo.InvariantCulture)}</v></c>");
                    return;

                case double number:
                    xml.Append(CultureInfo.InvariantCulture, $"<c r=\"{reference}\" s=\"{Decimal}\"><v>{number.ToString("R", CultureInfo.InvariantCulture)}</v></c>");
                    return;

                case DateOnly date:
                    xml.Append(CultureInfo.InvariantCulture, $"<c r=\"{reference}\" s=\"{DateStyle}\"><v>{date.DayNumber - ExcelEpoch.DayNumber}</v></c>");
                    return;

                case DateTimeOffset moment:
                    var days = (moment.UtcDateTime - ExcelEpoch.ToDateTime(TimeOnly.MinValue)).TotalDays;
                    xml.Append(CultureInfo.InvariantCulture, $"<c r=\"{reference}\" s=\"{DateTimeStyle}\"><v>{days.ToString("R", CultureInfo.InvariantCulture)}</v></c>");
                    return;

                default:
                    AppendText(xml, reference, ReportCells.Text(value), Default);
                    return;
            }
        }

        private static void AppendText(StringBuilder xml, string reference, string text, int style)
        {
            var styleAttribute = style == Default ? string.Empty : $" s=\"{style}\"";

            xml.Append("<c r=\"").Append(reference).Append('"').Append(styleAttribute).Append(" t=\"inlineStr\"><is><t xml:space=\"preserve\">")
               .Append(Escape(text)).Append("</t></is></c>");
        }

        // XML 1.0 cannot hold most control characters, and Excel holds at most 32767 characters in a cell.
        private static string Escape(string text)
        {
            var clean = new StringBuilder(Math.Min(text.Length, MaxCellText));

            foreach (var ch in text.Length > MaxCellText ? text[..MaxCellText] : text)
            {
                if (!XmlConvert.IsXmlChar(ch) && !char.IsSurrogate(ch))
                {
                    continue;
                }

                clean.Append(ch switch
                {
                    '&' => "&amp;",
                    '<' => "&lt;",
                    '>' => "&gt;",
                    '"' => "&quot;",
                    _ => ch.ToString()
                });
            }

            return clean.ToString();
        }

        // 0 -> A, 25 -> Z, 26 -> AA
        private static string ColumnName(int index)
        {
            var name = string.Empty;

            for (var n = index; n >= 0; n = n / 26 - 1)
            {
                name = (char)('A' + (n % 26)) + name;
            }

            return name;
        }

        private static void Add(ZipArchive zip, string path, string content)
        {
            var entry = zip.CreateEntry(path, CompressionLevel.Optimal);
            using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
            writer.Write(content);
        }

        private const string XmlDeclaration = "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>";

        private static readonly string ContentTypes = XmlDeclaration +
            "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">" +
            "<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>" +
            "<Default Extension=\"xml\" ContentType=\"application/xml\"/>" +
            "<Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/>" +
            "<Override PartName=\"/xl/worksheets/sheet1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>" +
            "<Override PartName=\"/xl/worksheets/sheet2.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>" +
            "<Override PartName=\"/xl/styles.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml\"/>" +
            "</Types>";

        private static readonly string RootRelationships = XmlDeclaration +
            "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
            "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/>" +
            "</Relationships>";

        private static readonly string Workbook = XmlDeclaration +
            "<workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\">" +
            "<sheets><sheet name=\"Report\" sheetId=\"1\" r:id=\"rId1\"/><sheet name=\"Info\" sheetId=\"2\" r:id=\"rId2\"/></sheets>" +
            "</workbook>";

        private static readonly string WorkbookRelationships = XmlDeclaration +
            "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
            "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet1.xml\"/>" +
            "<Relationship Id=\"rId2\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet2.xml\"/>" +
            "<Relationship Id=\"rId3\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles\" Target=\"styles.xml\"/>" +
            "</Relationships>";

        // cellXfs: 0 default, 1 header, 2 integer, 3 two decimals, 4 date, 5 date and time, 6 bold.
        private static readonly string Styles = XmlDeclaration +
            "<styleSheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">" +
            "<numFmts count=\"2\"><numFmt numFmtId=\"164\" formatCode=\"yyyy\\-mm\\-dd\"/><numFmt numFmtId=\"165\" formatCode=\"yyyy\\-mm\\-dd\\ hh:mm:ss\"/></numFmts>" +
            "<fonts count=\"2\"><font><sz val=\"11\"/><name val=\"Calibri\"/></font><font><b/><sz val=\"11\"/><name val=\"Calibri\"/></font></fonts>" +
            "<fills count=\"3\"><fill><patternFill patternType=\"none\"/></fill><fill><patternFill patternType=\"gray125\"/></fill><fill><patternFill patternType=\"solid\"><fgColor rgb=\"FFE7E6E6\"/><bgColor indexed=\"64\"/></patternFill></fill></fills>" +
            "<borders count=\"1\"><border><left/><right/><top/><bottom/><diagonal/></border></borders>" +
            "<cellStyleXfs count=\"1\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\"/></cellStyleXfs>" +
            "<cellXfs count=\"7\">" +
            "<xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\"/>" +
            "<xf numFmtId=\"0\" fontId=\"1\" fillId=\"2\" borderId=\"0\" xfId=\"0\" applyFont=\"1\" applyFill=\"1\"/>" +
            "<xf numFmtId=\"1\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyNumberFormat=\"1\"/>" +
            "<xf numFmtId=\"4\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyNumberFormat=\"1\"/>" +
            "<xf numFmtId=\"164\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyNumberFormat=\"1\"/>" +
            "<xf numFmtId=\"165\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyNumberFormat=\"1\"/>" +
            "<xf numFmtId=\"0\" fontId=\"1\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyFont=\"1\"/>" +
            "</cellXfs>" +
            "<cellStyles count=\"1\"><cellStyle name=\"Normal\" xfId=\"0\" builtinId=\"0\"/></cellStyles>" +
            "</styleSheet>";
    }




}
