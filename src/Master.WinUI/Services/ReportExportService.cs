using System.Globalization;
using System.Text;
using ClosedXML.Excel;

namespace Master_WinUI.Services;

public sealed class ReportExportService
{
    private const int PdfColumnsPerPage = 8;
    private const double PdfMargin = 32;
    private const double PdfHeaderHeight = 28;
    private const double PdfRowHeight = 22;
    private const double PdfA3LandscapeWidth = 1190.55;
    private const double PdfA3LandscapeHeight = 841.89;
    private static readonly Encoding PdfTextEncoding = CreatePdfTextEncoding();

    public byte[] BuildExcel(ReportCatalogResult result)
    {
        if (result.Columns.Count == 0)
        {
            throw new InvalidOperationException("El reporte no contiene columnas para exportar.");
        }

        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add(SanitizeWorksheetName(result.Definition.Name));
        var columnCount = result.Columns.Count;
        const int titleRow = 1;
        const int generatedRow = 2;
        const int headerRow = 4;
        var lastDataRow = headerRow + result.Rows.Count;

        worksheet.Range(titleRow, 1, titleRow, columnCount).Merge();
        worksheet.Cell(titleRow, 1).Value = result.Definition.Name;
        worksheet.Cell(titleRow, 1).Style.Font.Bold = true;
        worksheet.Cell(titleRow, 1).Style.Font.FontSize = 16;
        worksheet.Cell(generatedRow, 1).Value = $"Generado: {DateTime.Now:yyyy-MM-dd HH:mm:ss}";

        for (var column = 0; column < columnCount; column++)
        {
            worksheet.Cell(headerRow, column + 1).Value = result.Columns[column];
        }

        for (var row = 0; row < result.Rows.Count; row++)
        {
            for (var column = 0; column < columnCount; column++)
            {
                worksheet.Cell(headerRow + row + 1, column + 1).Value = result.Rows[row][column];
            }
        }

        var headerRange = worksheet.Range(headerRow, 1, lastDataRow, columnCount);
        headerRange.SetAutoFilter();
        headerRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        var header = worksheet.Range(headerRow, 1, headerRow, columnCount);
        header.Style.Font.Bold = true;
        header.Style.Font.FontColor = XLColor.White;
        header.Style.Fill.BackgroundColor = XLColor.FromHtml("#263B5A");
        header.Style.Alignment.WrapText = true;
        worksheet.SheetView.FreezeRows(headerRow);

        for (var column = 0; column < columnCount; column++)
        {
            var maxLength = result.Rows
                .Select(row => row[column].Length)
                .Append(result.Columns[column].Length)
                .DefaultIfEmpty(12)
                .Max();
            worksheet.Column(column + 1).Width = Math.Clamp(maxLength + 2, 12, 42);
        }

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    public byte[] BuildPdf(ReportCatalogResult result)
    {
        if (result.Columns.Count == 0)
        {
            throw new InvalidOperationException("El reporte no contiene columnas para exportar.");
        }

        var columnsPerPage = Math.Min(PdfColumnsPerPage, result.Columns.Count);
        var columnGroups = Enumerable.Range(0, result.Columns.Count)
            .Chunk(columnsPerPage)
            .ToArray();
        var rowCountPerPage = GetPdfRowsPerPage();
        var rowPageCount = Math.Max(1, (int)Math.Ceiling(result.Rows.Count / (double)rowCountPerPage));
        var pageCount = columnGroups.Length * rowPageCount;
        var pageContents = new List<byte[]>(pageCount);
        foreach (var columnGroup in columnGroups)
        {
            for (var rowPage = 0; rowPage < rowPageCount; rowPage++)
            {
                var pageStart = rowPage * rowCountPerPage;
                var pageEnd = Math.Min(pageStart + rowCountPerPage, result.Rows.Count);
                var pageNumber = pageContents.Count + 1;
                pageContents.Add(Encoding.ASCII.GetBytes(BuildPdfPageContent(
                    result,
                    columnGroup,
                    columnGroups.Length,
                    pageStart,
                    pageEnd,
                    result.Rows.Count,
                    pageNumber,
                    pageCount)));
            }
        }

        return BuildPdfDocument(pageContents, result.Definition.Name);
    }

    public byte[] BuildCsv(ReportCatalogResult result)
    {
        static string Escape(string value) =>
            $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";

        var lines = new List<string>
        {
            string.Join(",", result.Columns.Select(Escape))
        };
        lines.AddRange(result.Rows.Select(row => string.Join(",", row.Select(Escape))));
        return new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: true)
            .GetPreamble()
            .Concat(Encoding.UTF8.GetBytes(string.Join(Environment.NewLine, lines)))
            .ToArray();
    }

    private static int GetPdfRowsPerPage()
    {
        const double titleAndHeaderArea = PdfMargin + 62 + PdfHeaderHeight + PdfMargin + 16;
        var availableHeight = PdfA3LandscapeHeight - titleAndHeaderArea;
        return Math.Max(1, (int)(availableHeight / PdfRowHeight));
    }

    private static string SanitizeWorksheetName(string name)
    {
        var sanitized = string.Concat(name.Select(character =>
            character is ':' or '\\' or '/' or '?' or '*' or '[' or ']'
                ? '_'
                : character));
        return sanitized.Length <= 31 ? sanitized : sanitized[..31];
    }

    private static string BuildPdfPageContent(
        ReportCatalogResult result,
        IReadOnlyList<int> columnGroup,
        int columnGroupCount,
        int pageStart,
        int pageEnd,
        int totalRows,
        int pageNumber,
        int pageCount)
    {
        const double titleTop = PdfMargin;
        const double tableTop = PdfMargin + 62;
        var contentWidth = PdfA3LandscapeWidth - PdfMargin * 2;
        var columnWidth = contentWidth / columnGroup.Count;
        var content = new StringBuilder("q\n");
        AppendPdfText(content, result.Definition.Name, PdfMargin, titleTop, 15, bold: true, contentWidth);
        var sectionText = columnGroupCount > 1
            ? $"Generado: {DateTime.Now:yyyy-MM-dd HH:mm:ss} | Columnas {columnGroup[0] + 1}-{columnGroup[^1] + 1} de {result.Columns.Count}"
            : $"Generado: {DateTime.Now:yyyy-MM-dd HH:mm:ss}";
        AppendPdfText(content, sectionText, PdfMargin, titleTop + 29, 8, bold: false, contentWidth);

        for (var columnIndex = 0; columnIndex < columnGroup.Count; columnIndex++)
        {
            var x = PdfMargin + columnIndex * columnWidth;
            AppendPdfRectangle(content, x, PdfA3LandscapeHeight - tableTop - PdfHeaderHeight, columnWidth, PdfHeaderHeight, "0.15 0.23 0.35");
            AppendPdfText(content, result.Columns[columnGroup[columnIndex]], x + 4, tableTop + 7, 8, bold: true, columnWidth - 8, white: true);
        }

        for (var rowIndex = pageStart; rowIndex < pageEnd; rowIndex++)
        {
            var rowOffset = rowIndex - pageStart;
            var top = tableTop + PdfHeaderHeight + rowOffset * PdfRowHeight;
            var fill = rowOffset % 2 == 0 ? "1 1 1" : "0.94 0.95 0.96";
            for (var columnIndex = 0; columnIndex < columnGroup.Count; columnIndex++)
            {
                var x = PdfMargin + columnIndex * columnWidth;
                AppendPdfRectangle(content, x, PdfA3LandscapeHeight - top - PdfRowHeight, columnWidth, PdfRowHeight, fill);
                AppendPdfCellBorder(content, x, PdfA3LandscapeHeight - top - PdfRowHeight, columnWidth, PdfRowHeight);
                AppendPdfText(content, result.Rows[rowIndex][columnGroup[columnIndex]], x + 4, top + 6, 8, bold: false, columnWidth - 8);
            }
        }

        var rowRange = totalRows == 0 ? "Sin registros" : $"Filas {pageStart + 1}-{pageEnd} de {totalRows}";
        AppendPdfText(content, $"Página {pageNumber} de {pageCount} • {rowRange}", PdfMargin, PdfA3LandscapeHeight - PdfMargin - 12, 8, bold: false, contentWidth);
        content.Append("Q\n");
        return content.ToString();
    }

    private static void AppendPdfText(
        StringBuilder content,
        string value,
        double x,
        double top,
        double fontSize,
        bool bold,
        double maxWidth,
        bool white = false)
    {
        var maxCharacters = Math.Max(0, (int)(maxWidth / (fontSize * 0.55)));
        var text = value.Length <= maxCharacters
            ? value
            : value[..Math.Max(0, maxCharacters - 3)] + "...";
        var baseline = PdfA3LandscapeHeight - top - fontSize;
        content.Append("BT\n");
        content.Append(white ? "1 1 1 rg\n" : "0 0 0 rg\n");
        content.Append(bold ? "/F2 " : "/F1 ");
        content.Append(fontSize.ToString("0.##", CultureInfo.InvariantCulture));
        content.Append(" Tf\n");
        content.Append(x.ToString("0.##", CultureInfo.InvariantCulture));
        content.Append(' ');
        content.Append(baseline.ToString("0.##", CultureInfo.InvariantCulture));
        content.Append(" Td <");
        content.Append(ToPdfHexString(text));
        content.Append("> Tj\nET\n");
    }

    private static void AppendPdfRectangle(
        StringBuilder content,
        double x,
        double y,
        double width,
        double height,
        string fill)
    {
        content.Append(fill);
        content.Append(" rg ");
        content.Append(FormatPdfNumber(x));
        content.Append(' ');
        content.Append(FormatPdfNumber(y));
        content.Append(' ');
        content.Append(FormatPdfNumber(width));
        content.Append(' ');
        content.Append(FormatPdfNumber(height));
        content.Append(" re f\n");
    }

    private static void AppendPdfCellBorder(StringBuilder content, double x, double y, double width, double height)
    {
        content.Append("0.75 0.78 0.82 RG 0.5 w ");
        content.Append(FormatPdfNumber(x));
        content.Append(' ');
        content.Append(FormatPdfNumber(y));
        content.Append(' ');
        content.Append(FormatPdfNumber(width));
        content.Append(' ');
        content.Append(FormatPdfNumber(height));
        content.Append(" re S\n");
    }

    private static byte[] BuildPdfDocument(IReadOnlyList<byte[]> pageContents, string title)
    {
        var objects = new List<byte[]>();
        static byte[] Ascii(string value) => Encoding.ASCII.GetBytes(value);
        objects.Add(Ascii("<< /Type /Catalog /Pages 2 0 R >>"));
        var pageReferences = string.Join(" ", Enumerable.Range(0, pageContents.Count)
            .Select(index => $"{5 + index * 2} 0 R"));
        objects.Add(Ascii($"<< /Type /Pages /Kids [{pageReferences}] /Count {pageContents.Count} >>"));
        objects.Add(Ascii("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>"));
        objects.Add(Ascii("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold /Encoding /WinAnsiEncoding >>"));

        for (var index = 0; index < pageContents.Count; index++)
        {
            var pageObjectNumber = 5 + index * 2;
            var contentObjectNumber = pageObjectNumber + 1;
            objects.Add(Ascii(
                $"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {FormatPdfNumber(PdfA3LandscapeWidth)} {FormatPdfNumber(PdfA3LandscapeHeight)}] "
                + $"/Resources << /Font << /F1 3 0 R /F2 4 0 R >> >> /Contents {contentObjectNumber} 0 R >>"));
            var streamPrefix = Ascii($"<< /Length {pageContents[index].Length} >>\nstream\n");
            var streamSuffix = Ascii("\nendstream");
            objects.Add(streamPrefix.Concat(pageContents[index]).Concat(streamSuffix).ToArray());
        }

        var infoObjectNumber = objects.Count + 1;
        objects.Add(Ascii($"<< /Title <{ToPdfHexString(title)}> >>"));
        using var output = new MemoryStream();
        WritePdfAscii(output, "%PDF-1.4\n");
        var offsets = new List<long> { 0 };
        for (var index = 0; index < objects.Count; index++)
        {
            offsets.Add(output.Position);
            WritePdfAscii(output, $"{index + 1} 0 obj\n");
            output.Write(objects[index]);
            WritePdfAscii(output, "\nendobj\n");
        }

        var crossReferenceOffset = output.Position;
        WritePdfAscii(output, $"xref\n0 {objects.Count + 1}\n0000000000 65535 f \n");
        foreach (var offset in offsets.Skip(1))
        {
            WritePdfAscii(output, $"{offset:0000000000} 00000 n \n");
        }

        WritePdfAscii(output,
            $"trailer\n<< /Size {objects.Count + 1} /Root 1 0 R /Info {infoObjectNumber} 0 R >>\n"
            + $"startxref\n{crossReferenceOffset}\n%%EOF");
        return output.ToArray();
    }

    private static string ToPdfHexString(string value)
    {
        return Convert.ToHexString(PdfTextEncoding.GetBytes(value));
    }

    private static Encoding CreatePdfTextEncoding()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        return Encoding.GetEncoding(1252);
    }

    private static string FormatPdfNumber(double value) =>
        value.ToString("0.##", CultureInfo.InvariantCulture);

    private static void WritePdfAscii(Stream stream, string value)
    {
        var bytes = Encoding.ASCII.GetBytes(value);
        stream.Write(bytes, 0, bytes.Length);
    }
}
