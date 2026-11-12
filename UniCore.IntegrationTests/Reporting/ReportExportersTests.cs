using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using UniCore.Application.Modules.Reporting;
using UniCore.Infrastructure.Modules.Reporting.Export;

namespace UniCore.IntegrationTests.Reporting
{
    // The three file formats, written without a library, checked by reading the files back the way a reader would.
    public class ReportExportersTests
    {
        private static readonly XNamespace Main = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        private static readonly DateTimeOffset Generated = new(2026, 11, 8, 12, 0, 0, TimeSpan.Zero);

        private static readonly ReportColumn[] Columns =
        [
            new("text", "Text", ReportColumnType.Text),
            new("count", "Count", ReportColumnType.Integer),
            new("amount", "Amount", ReportColumnType.Decimal),
            new("day", "Day", ReportColumnType.Date),
            new("at", "At", ReportColumnType.DateTime),
            new("flag", "Flag", ReportColumnType.Boolean)
        ];

        private static ReportExportModel Model(IEnumerable<object?[]> rows, IReadOnlyList<ReportColumn>? columns = null)
        {
            var list = rows.Select(r => (IReadOnlyList<object?>)r).ToList();

            return new ReportExportModel("Test report", "A report for tests", "from=2026-01-01", Generated, new ReportTable(columns ?? Columns, list, list.Count));
        }

        private static readonly object?[][] Sample =
        [
            ["Plain", 5, 1234.5m, new DateOnly(2026, 11, 8), new DateTimeOffset(2026, 11, 8, 10, 30, 15, TimeSpan.Zero), true],
            ["=1+1", -3, -0.5m, null, null, false],
            ["comma, \"quote\"\nline", 0, 0m, new DateOnly(2000, 1, 1), null, null],
            ["Arabic \u0639\u0631\u0628\u064a", 1_000_000L, 99999999.99m, null, null, true],
            ["-minus", 1, 1m, null, null, true]
        ];

        // ---------- CSV ----------

        private static List<List<string>> ReadCsv(byte[] bytes)
        {
            var text = Encoding.UTF8.GetString(bytes).TrimStart('\uFEFF');
            var rows = new List<List<string>>();
            var row = new List<string>();
            var cell = new StringBuilder();
            var quoted = false;

            for (var i = 0; i < text.Length; i++)
            {
                var ch = text[i];

                if (quoted)
                {
                    if (ch == '"' && i + 1 < text.Length && text[i + 1] == '"')
                    {
                        cell.Append('"');
                        i++;
                    }
                    else if (ch == '"')
                    {
                        quoted = false;
                    }
                    else
                    {
                        cell.Append(ch);
                    }
                }
                else if (ch == '"')
                {
                    quoted = true;
                }
                else if (ch == ',')
                {
                    row.Add(cell.ToString());
                    cell.Clear();
                }
                else if (ch == '\r')
                {
                    row.Add(cell.ToString());
                    cell.Clear();
                    rows.Add(row);
                    row = [];
                    i++;
                }
                else
                {
                    cell.Append(ch);
                }
            }

            return rows;
        }

        [Fact]
        public void Csv_starts_with_a_byte_order_mark_and_has_a_header_row_and_crlf_line_ends()
        {
            var bytes = new CsvReportExporter().Export(Model(Sample));

            Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, bytes[..3]);
            var rows = ReadCsv(bytes);
            Assert.Equal(["Text", "Count", "Amount", "Day", "At", "Flag"], rows[0]);
            Assert.Equal(6, rows.Count);
            Assert.All(rows, r => Assert.Equal(6, r.Count));
        }

        [Fact]
        public void Csv_writes_numbers_dates_and_booleans_in_a_fixed_format()
        {
            var rows = ReadCsv(new CsvReportExporter().Export(Model(Sample)));

            Assert.Equal(["Plain", "5", "1234.50", "2026-11-08", "2026-11-08 10:30:15", "true"], rows[1]);
            Assert.Equal(["=1+1".Insert(0, "'"), "-3", "-0.50", "", "", "false"], rows[2]);
            Assert.Equal("99999999.99", rows[4][2]);
        }

        [Fact]
        public void Csv_quotes_commas_quotes_and_line_breaks_and_keeps_other_alphabets()
        {
            var rows = ReadCsv(new CsvReportExporter().Export(Model(Sample)));

            Assert.Equal("comma, \"quote\"\nline", rows[3][0]);
            Assert.Equal("Arabic \u0639\u0631\u0628\u064a", rows[4][0]);
        }

        [Theory]
        [InlineData("=SUM(A1:A9)")]
        [InlineData("+cmd|' /C calc'!A0")]
        [InlineData("-2+3")]
        [InlineData("@SUM(1)")]
        [InlineData("\tcmd")]
        public void Csv_defuses_text_that_a_spreadsheet_would_run_as_a_formula(string dangerous)
        {
            var rows = ReadCsv(new CsvReportExporter().Export(Model([[dangerous, 1, 1m, null, null, null]])));

            Assert.StartsWith("'", rows[1][0]);
            Assert.EndsWith(dangerous, rows[1][0]);
        }

        [Fact]
        public void Csv_does_not_touch_negative_numbers_and_harmless_text()
        {
            var rows = ReadCsv(new CsvReportExporter().Export(Model([["a=b", -5, -1.5m, null, null, null]])));

            Assert.Equal(["a=b", "-5", "-1.50", "", "", ""], rows[1]);
        }

        [Fact]
        public void Csv_of_an_empty_report_is_just_the_header()
        {
            var rows = ReadCsv(new CsvReportExporter().Export(Model([])));

            Assert.Single(rows);
        }

        // ---------- XLSX ----------

        private static Dictionary<string, XDocument> ReadWorkbook(byte[] bytes)
        {
            using var zip = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);

            return zip.Entries.ToDictionary(e => e.FullName, e =>
            {
                using var stream = e.Open();
                return XDocument.Load(stream);
            });
        }

        private static XElement Cell(XDocument sheet, string reference)
            => sheet.Descendants(Main + "c").Single(c => (string?)c.Attribute("r") == reference);

        [Fact]
        public void Xlsx_is_a_zip_of_well_formed_office_parts()
        {
            var bytes = new XlsxReportExporter().Export(Model(Sample));
            var parts = ReadWorkbook(bytes);

            Assert.Equal(new byte[] { (byte)'P', (byte)'K' }, bytes[..2]);
            foreach (var name in new[] { "[Content_Types].xml", "_rels/.rels", "xl/workbook.xml", "xl/_rels/workbook.xml.rels", "xl/styles.xml", "xl/worksheets/sheet1.xml", "xl/worksheets/sheet2.xml" })
            {
                Assert.True(parts.ContainsKey(name), $"missing part {name}");
            }
        }

        [Fact]
        public void Xlsx_report_sheet_has_a_header_real_numbers_dates_and_booleans()
        {
            var sheet = ReadWorkbook(new XlsxReportExporter().Export(Model(Sample)))["xl/worksheets/sheet1.xml"];

            Assert.Equal("Text", Cell(sheet, "A1").Value);
            Assert.Equal("Flag", Cell(sheet, "F1").Value);
            Assert.Equal("5", Cell(sheet, "B2").Element(Main + "v")!.Value);
            Assert.Equal("1234.5", Cell(sheet, "C2").Element(Main + "v")!.Value);
            Assert.Equal("46334", Cell(sheet, "D2").Element(Main + "v")!.Value);
            Assert.Equal("b", (string?)Cell(sheet, "F2").Attribute("t"));
            Assert.Equal("1", Cell(sheet, "F2").Element(Main + "v")!.Value);
            Assert.Equal("-3", Cell(sheet, "B3").Element(Main + "v")!.Value);
            Assert.Equal("1000000", Cell(sheet, "B5").Element(Main + "v")!.Value);
        }

        [Fact]
        public void Xlsx_keeps_text_as_text_even_when_it_looks_like_a_formula()
        {
            var sheet = ReadWorkbook(new XlsxReportExporter().Export(Model(Sample)))["xl/worksheets/sheet1.xml"];
            var formula = Cell(sheet, "A3");

            Assert.Equal("inlineStr", (string?)formula.Attribute("t"));
            Assert.Equal("=1+1", formula.Value);
            Assert.Empty(sheet.Descendants(Main + "f"));
        }

        [Fact]
        public void Xlsx_keeps_other_alphabets_line_breaks_and_special_characters()
        {
            var sheet = ReadWorkbook(new XlsxReportExporter().Export(Model(Sample)))["xl/worksheets/sheet1.xml"];

            Assert.Equal("comma, \"quote\"\nline", Cell(sheet, "A4").Value);
            Assert.Equal("Arabic \u0639\u0631\u0628\u064a", Cell(sheet, "A5").Value);
        }

        [Fact]
        public void Xlsx_leaves_out_empty_cells_and_freezes_and_filters_the_header()
        {
            var sheet = ReadWorkbook(new XlsxReportExporter().Export(Model(Sample)))["xl/worksheets/sheet1.xml"];

            Assert.Empty(sheet.Descendants(Main + "c").Where(c => (string?)c.Attribute("r") == "D3"));
            Assert.Equal("frozen", (string?)sheet.Descendants(Main + "pane").Single().Attribute("state"));
            Assert.Equal("A1:F6", (string?)sheet.Descendants(Main + "autoFilter").Single().Attribute("ref"));
        }

        [Fact]
        public void Xlsx_strips_characters_that_xml_cannot_hold()
        {
            var sheet = ReadWorkbook(new XlsxReportExporter().Export(Model([["bad\u0001char\u0008here & <tag>", 1, 1m, null, null, null]])))["xl/worksheets/sheet1.xml"];

            Assert.Equal("badcharhere & <tag>", Cell(sheet, "A2").Value);
        }

        [Fact]
        public void Xlsx_info_sheet_describes_the_report()
        {
            var info = ReadWorkbook(new XlsxReportExporter().Export(Model(Sample)))["xl/worksheets/sheet2.xml"];
            var texts = info.Descendants(Main + "t").Select(t => t.Value).ToList();

            Assert.Contains("Test report", texts);
            Assert.Contains("from=2026-01-01", texts);
            Assert.Contains("2026-11-08 12:00:00", texts);
            Assert.Contains("5", texts);
        }

        [Fact]
        public void Xlsx_names_columns_past_Z_correctly()
        {
            var columns = Enumerable.Range(0, 30).Select(i => new ReportColumn($"c{i}", $"C{i}", ReportColumnType.Text)).ToArray();
            var row = Enumerable.Range(0, 30).Select(i => (object?)$"v{i}").ToArray();

            var sheet = ReadWorkbook(new XlsxReportExporter().Export(Model([row], columns)))["xl/worksheets/sheet1.xml"];

            Assert.Equal("C25", Cell(sheet, "Z1").Value);
            Assert.Equal("C26", Cell(sheet, "AA1").Value);
            Assert.Equal("C29", Cell(sheet, "AD1").Value);
            Assert.Equal("v29", Cell(sheet, "AD2").Value);
        }

        [Fact]
        public void Xlsx_of_an_empty_report_still_opens()
        {
            var sheet = ReadWorkbook(new XlsxReportExporter().Export(Model([])))["xl/worksheets/sheet1.xml"];

            Assert.Equal("Text", Cell(sheet, "A1").Value);
            Assert.Equal("A1:F1", (string?)sheet.Descendants(Main + "autoFilter").Single().Attribute("ref"));
        }

        // ---------- PDF ----------

        private static string Pdf(byte[] bytes) => Encoding.Latin1.GetString(bytes);

        private static int Pages(string pdf) => Regex.Matches(pdf, @"/Type /Page ").Count;

        [Fact]
        public void Pdf_has_a_header_a_trailer_and_a_cross_reference_table_that_points_at_every_object()
        {
            var pdf = Pdf(new PdfReportExporter().Export(Model(Sample)));

            Assert.StartsWith("%PDF-1.4", pdf);
            Assert.EndsWith("%%EOF\n", pdf);

            var startXref = int.Parse(Regex.Match(pdf, @"startxref\n(\d+)\n").Groups[1].Value);
            Assert.Equal("xref", pdf.Substring(startXref, 4));

            var entries = Regex.Matches(pdf[startXref..], @"(\d{10}) \d{5} n \n");
            Assert.True(entries.Count >= 7);

            for (var i = 0; i < entries.Count; i++)
            {
                var offset = int.Parse(entries[i].Groups[1].Value);
                Assert.StartsWith($"{i + 1} 0 obj", pdf[offset..]);
            }
        }

        [Fact]
        public void Pdf_stream_lengths_are_exact()
        {
            var pdf = Pdf(new PdfReportExporter().Export(Model(Sample)));

            foreach (Match match in Regex.Matches(pdf, @"/Length (\d+) >>\nstream\n"))
            {
                var length = int.Parse(match.Groups[1].Value);
                var start = match.Index + match.Length;

                Assert.Equal("\nendstream", pdf.Substring(start + length, 10));
            }
        }

        [Fact]
        public void Pdf_is_split_over_pages_and_every_page_has_the_footer()
        {
            var rows = Enumerable.Range(0, 150).Select(i => new object?[] { $"Row {i}", i, i * 1.5m, null, null, null }).ToList();

            var pdf = Pdf(new PdfReportExporter().Export(Model(rows)));
            var pages = Pages(pdf);

            Assert.True(pages >= 4, $"expected several pages, got {pages}");

            for (var p = 1; p <= pages; p++)
            {
                Assert.Contains($"(Page {p} of {pages})", pdf);
            }

            Assert.Contains("(Row 149)", pdf);
        }

        [Fact]
        public void Pdf_repeats_the_header_row_on_every_page()
        {
            var rows = Enumerable.Range(0, 150).Select(i => new object?[] { $"Row {i}", i, i * 1.5m, null, null, null }).ToList();

            var pdf = Pdf(new PdfReportExporter().Export(Model(rows)));

            Assert.Equal(Pages(pdf), Regex.Matches(pdf, @"\(Count\) Tj").Count);
        }

        [Fact]
        public void Pdf_escapes_parentheses_and_backslashes_and_prints_unsupported_letters_as_a_question_mark()
        {
            var pdf = Pdf(new PdfReportExporter().Export(Model([["a (b) \\ c \u0639\u0631\u0628\u064a", 1, 1m, null, null, null]])));

            Assert.Contains(@"a \(b\) \\ c ????", pdf);
        }

        [Fact]
        public void Pdf_keeps_western_european_letters()
        {
            var pdf = Pdf(new PdfReportExporter().Export(Model([["caf\u00e9", 1, 1m, null, null, null]])));

            Assert.Contains(@"caf\351", pdf);
        }

        [Fact]
        public void Pdf_cuts_text_that_is_too_long_for_its_column()
        {
            var longText = new string('x', 500);

            var pdf = Pdf(new PdfReportExporter().Export(Model([[longText, 1, 1m, null, null, null]])));

            Assert.DoesNotContain(longText, pdf);
            Assert.Contains("...)", pdf);
        }

        [Fact]
        public void Pdf_of_an_empty_report_is_a_single_page()
        {
            var pdf = Pdf(new PdfReportExporter().Export(Model([])));

            Assert.Equal(1, Pages(pdf));
            Assert.Contains("(Page 1 of 1)", pdf);
        }

        // ---------- all formats ----------

        [Fact]
        public void The_formats_declare_their_names_and_limits()
        {
            var exporters = new IReportExporter[] { new CsvReportExporter(), new XlsxReportExporter(), new PdfReportExporter() };

            Assert.Equal([ReportFormat.Csv, ReportFormat.Xlsx, ReportFormat.Pdf], exporters.Select(e => e.Format));
            Assert.Equal(["csv", "xlsx", "pdf"], exporters.Select(e => e.Extension));
            Assert.True(exporters[2].MaxRows < exporters[1].MaxRows);
            Assert.All(exporters, e => Assert.False(string.IsNullOrEmpty(e.ContentType)));
        }
    }




}
