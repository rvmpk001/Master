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

        if (result.Definition.Id is "production" or "production-real")
        {
            return BuildProductionExcel(result);
        }

        if (result.Definition.Id == FormulaReportLayout.ReportId)
        {
            return BuildFormulaExcel(result);
        }

        if (result.Definition.Id == "inventory-stock")
        {
            return BuildInventoryStockExcel(result);
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

        if (result.Definition.Id is "production" or "production-real")
        {
            return BuildProductionPdf(result);
        }

        if (result.Definition.Id == FormulaReportLayout.ReportId)
        {
            return BuildFormulaPdf(result);
        }

        if (result.Definition.Id == "inventory-stock")
        {
            result = ExpandInventoryStockForPdf(result);
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

    private static byte[] BuildInventoryStockExcel(ReportCatalogResult result)
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add(SanitizeWorksheetName(result.Definition.Name));
        var columnCount = result.Columns.Count * 2;
        const int titleRow = 1;
        const int generatedRow = 2;
        const int categoryHeaderRow = 4;
        const int fieldHeaderRow = 5;
        var lastDataRow = fieldHeaderRow + result.Rows.Count;

        worksheet.Range(titleRow, 1, titleRow, columnCount).Merge();
        worksheet.Cell(titleRow, 1).Value = result.Definition.Name;
        worksheet.Cell(titleRow, 1).Style.Font.Bold = true;
        worksheet.Cell(titleRow, 1).Style.Font.FontSize = 16;
        worksheet.Cell(generatedRow, 1).Value = $"Fecha de corte: {DateTime.Now:dd/MM/yyyy HH:mm:ss}";

        for (var group = 0; group < result.Columns.Count; group++)
        {
            var firstColumn = group * 2 + 1;
            var groupHeader = worksheet.Range(categoryHeaderRow, firstColumn, categoryHeaderRow, firstColumn + 1);
            groupHeader.Merge();
            groupHeader.FirstCell().Value = result.Columns[group];
            groupHeader.Style.Font.Bold = true;
            groupHeader.Style.Font.FontColor = XLColor.White;
            groupHeader.Style.Fill.BackgroundColor = XLColor.FromHtml("#263B5A");
            groupHeader.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            worksheet.Cell(fieldHeaderRow, firstColumn).Value = "Material";
            worksheet.Cell(fieldHeaderRow, firstColumn + 1).Value = "Existencia";
        }

        for (var row = 0; row < result.Rows.Count; row++)
        {
            for (var group = 0; group < result.Columns.Count; group++)
            {
                var (material, quantity) = SplitInventoryStockValue(result.Rows[row][group]);
                var firstColumn = group * 2 + 1;
                worksheet.Cell(fieldHeaderRow + row + 1, firstColumn).Value = material;
                worksheet.Cell(fieldHeaderRow + row + 1, firstColumn + 1).Value = quantity;
            }
        }

        var tableRange = worksheet.Range(fieldHeaderRow, 1, lastDataRow, columnCount);
        tableRange.SetAutoFilter();
        tableRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        var fieldHeader = worksheet.Range(fieldHeaderRow, 1, fieldHeaderRow, columnCount);
        fieldHeader.Style.Font.Bold = true;
        fieldHeader.Style.Font.FontColor = XLColor.White;
        fieldHeader.Style.Fill.BackgroundColor = XLColor.FromHtml("#40516A");
        worksheet.SheetView.FreezeRows(fieldHeaderRow);
        for (var group = 0; group < result.Columns.Count; group++)
        {
            worksheet.Column(group * 2 + 1).Width = 28;
            worksheet.Column(group * 2 + 2).Width = 14;
        }

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static byte[] BuildFormulaExcel(ReportCatalogResult result)
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add(SanitizeWorksheetName(result.Definition.Name));
        var outputColumns = result.Columns
            .Select((column, index) => (Column: column, Index: index, IsCategory: FormulaReportLayout.TryGetCategory(column, out _)))
            .ToArray();
        var columnCount = outputColumns.Sum(item => item.IsCategory ? 2 : 1);
        const int titleRow = 1;
        const int generatedRow = 2;
        const int groupHeaderRow = 4;
        const int fieldHeaderRow = 5;
        var lastDataRow = fieldHeaderRow + result.Rows.Count;

        worksheet.Range(titleRow, 1, titleRow, columnCount).Merge();
        worksheet.Cell(titleRow, 1).Value = result.Definition.Name.ToUpperInvariant();
        worksheet.Cell(titleRow, 1).Style.Font.Bold = true;
        worksheet.Cell(titleRow, 1).Style.Font.FontSize = 16;
        worksheet.Cell(generatedRow, 1).Value = $"Fecha y hora de impresión: {DateTime.Now:dd/MM/yyyy, HH:mm}";

        var outputColumn = 1;
        foreach (var item in outputColumns)
        {
            if (item.IsCategory && FormulaReportLayout.TryGetCategory(item.Column, out var category))
            {
                var groupHeader = worksheet.Range(groupHeaderRow, outputColumn, groupHeaderRow, outputColumn + 1);
                groupHeader.Merge();
                groupHeader.FirstCell().Value = item.Column;
                worksheet.Cell(fieldHeaderRow, outputColumn).Value = "Material";
                worksheet.Cell(fieldHeaderRow, outputColumn + 1).Value = $"Cantidad ({category.Unit})";
                worksheet.Column(outputColumn).Width = 24;
                worksheet.Column(outputColumn + 1).Width = 14;
                outputColumn += 2;
            }
            else
            {
                worksheet.Cell(groupHeaderRow, outputColumn).Value = item.Column;
                worksheet.Cell(fieldHeaderRow, outputColumn).Value =
                    item.Column.Contains("Código", StringComparison.OrdinalIgnoreCase) ? "Código" : "Detalle";
                worksheet.Column(outputColumn).Width = item.Column.Contains("Descripción", StringComparison.OrdinalIgnoreCase) ? 32 : 20;
                outputColumn++;
            }
        }

        for (var rowIndex = 0; rowIndex < result.Rows.Count; rowIndex++)
        {
            outputColumn = 1;
            var lineCount = 1;
            foreach (var item in outputColumns)
            {
                var value = result.Rows[rowIndex][item.Index];
                if (item.IsCategory)
                {
                    var entries = FormulaReportLayout.ParseEntries(value);
                    lineCount = Math.Max(lineCount, entries.Count);
                    worksheet.Cell(fieldHeaderRow + rowIndex + 1, outputColumn).Value =
                        string.Join(Environment.NewLine, entries.Select(entry => entry.Material));
                    worksheet.Cell(fieldHeaderRow + rowIndex + 1, outputColumn + 1).Value =
                        string.Join(Environment.NewLine, entries.Select(entry => entry.Quantity));
                    worksheet.Cell(fieldHeaderRow + rowIndex + 1, outputColumn).Style.Alignment.WrapText = true;
                    worksheet.Cell(fieldHeaderRow + rowIndex + 1, outputColumn + 1).Style.Alignment.WrapText = true;
                    outputColumn += 2;
                }
                else
                {
                    worksheet.Cell(fieldHeaderRow + rowIndex + 1, outputColumn).Value = value;
                    worksheet.Cell(fieldHeaderRow + rowIndex + 1, outputColumn).Style.Alignment.WrapText = true;
                    outputColumn++;
                }
            }

            worksheet.Row(fieldHeaderRow + rowIndex + 1).Height = Math.Max(18, lineCount * 15);
        }

        var headerRange = worksheet.Range(groupHeaderRow, 1, fieldHeaderRow, columnCount);
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Font.FontColor = XLColor.White;
        headerRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#263B5A");
        headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        headerRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        var dataRange = worksheet.Range(fieldHeaderRow, 1, lastDataRow, columnCount);
        dataRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Top;
        dataRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        dataRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
        worksheet.Range(fieldHeaderRow, 1, lastDataRow, columnCount).SetAutoFilter();
        worksheet.SheetView.FreezeRows(fieldHeaderRow);
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static byte[] BuildProductionExcel(ReportCatalogResult result)
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add(SanitizeWorksheetName(result.Definition.Name));
        var columnCount = result.Columns.Count;
        const int titleRow = 1;
        const int generatedRow = 2;
        const int headerRow = 4;
        var lastDataRow = headerRow + result.Rows.Count;

        worksheet.Range(titleRow, 1, titleRow, columnCount).Merge();
        worksheet.Cell(titleRow, 1).Value = "REPORTE DE CARGAS";
        worksheet.Cell(titleRow, 1).Style.Font.Bold = true;
        worksheet.Cell(titleRow, 1).Style.Font.FontSize = 18;
        worksheet.Cell(titleRow, 1).Style.Font.FontColor = XLColor.FromHtml("#F4773B");
        worksheet.Cell(generatedRow, 1).Value = $"Fecha: {DateTime.Now:dd 'de' MMMM 'de' yyyy}";

        for (var column = 0; column < columnCount; column++)
        {
            worksheet.Cell(headerRow, column + 1).Value = result.Columns[column];
            worksheet.Column(column + 1).Width = result.Columns[column] switch
            {
                "TIPO DE CONCRETO" => 25,
                "OBRA Y RESIDENTE" => 32,
                "UNIDAD" or "ELEMENTO" => 16,
                "SALIDA PLANTA" or "LLEGADA PLANTA" => 16,
                _ => 13
            };
        }

        for (var row = 0; row < result.Rows.Count; row++)
        {
            for (var column = 0; column < columnCount; column++)
            {
                worksheet.Cell(headerRow + row + 1, column + 1).Value = result.Rows[row][column];
            }
        }

        var headerRange = worksheet.Range(headerRow, 1, headerRow, columnCount);
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Font.FontColor = XLColor.White;
        headerRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#F4773B");
        headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        headerRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        var tableRange = worksheet.Range(headerRow, 1, lastDataRow, columnCount);
        tableRange.SetAutoFilter();
        tableRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        tableRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        tableRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
        worksheet.SheetView.FreezeRows(headerRow);
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static byte[] BuildProductionPdf(ReportCatalogResult result)
    {
        const double titleTop = 34;
        const double tableTop = 94;
        const double headerHeight = 28;
        const double rowHeight = 20;
        const double margin = 32;
        const double footerHeight = 30;
        var columnWidths = result.Columns.Select(GetProductionPdfColumnWidth).ToArray();
        var tableWidth = columnWidths.Sum();
        var contentWidth = PdfA3LandscapeWidth - margin * 2;
        if (tableWidth > contentWidth)
        {
            var scale = contentWidth / tableWidth;
            for (var index = 0; index < columnWidths.Length; index++)
            {
                columnWidths[index] *= scale;
            }

            tableWidth = contentWidth;
        }

        var tableLeft = (PdfA3LandscapeWidth - tableWidth) / 2;
        var rowsPerPage = Math.Max(1, (int)((PdfA3LandscapeHeight - tableTop - headerHeight - margin - footerHeight) / rowHeight));
        var pageCount = Math.Max(1, (int)Math.Ceiling(result.Rows.Count / (double)rowsPerPage));
        var generatedAt = DateTime.Now;
        var pageContents = new List<byte[]>(pageCount);

        for (var pageIndex = 0; pageIndex < pageCount; pageIndex++)
        {
            var start = pageIndex * rowsPerPage;
            var end = Math.Min(start + rowsPerPage, result.Rows.Count);
            var content = new StringBuilder("q\n");
            AppendPdfText(content, "REPORTE DE CARGAS", margin, titleTop, 18, bold: true, contentWidth);
            AppendPdfText(
                content,
                $"Fecha: {generatedAt:dd 'de' MMMM 'de' yyyy}",
                margin,
                titleTop + 29,
                9,
                bold: false,
                contentWidth);

            var x = tableLeft;
            for (var column = 0; column < result.Columns.Count; column++)
            {
                var width = columnWidths[column];
                var y = PdfA3LandscapeHeight - tableTop - headerHeight;
                AppendPdfRectangle(content, x, y, width, headerHeight, "0.96 0.43 0.22");
                AppendPdfCellBorder(content, x, y, width, headerHeight);
                AppendPdfText(content, result.Columns[column], x + 3, tableTop + 9, 6.5, bold: true, width - 6, white: true);
                x += width;
            }

            var rowTop = tableTop + headerHeight;
            for (var rowIndex = start; rowIndex < end; rowIndex++)
            {
                var fill = (rowIndex - start) % 2 == 0 ? "1 1 1" : "0.96 0.96 0.96";
                x = tableLeft;
                for (var column = 0; column < result.Columns.Count; column++)
                {
                    var width = columnWidths[column];
                    var y = PdfA3LandscapeHeight - rowTop - rowHeight;
                    AppendPdfRectangle(content, x, y, width, rowHeight, fill);
                    AppendPdfCellBorder(content, x, y, width, rowHeight);
                    AppendPdfText(content, result.Rows[rowIndex][column], x + 3, rowTop + 6, 7, bold: false, width - 6);
                    x += width;
                }

                rowTop += rowHeight;
            }

            var rowRange = result.Rows.Count == 0
                ? "Sin registros"
                : $"Cargas {start + 1}-{end} de {result.Rows.Count}";
            AppendPdfText(content, $"Página {pageIndex + 1} de {pageCount} | {rowRange}", margin, PdfA3LandscapeHeight - margin - 12, 8, bold: false, contentWidth);
            content.Append("Q\n");
            pageContents.Add(Encoding.ASCII.GetBytes(content.ToString()));
        }

        return BuildPdfDocument(pageContents, "Reporte de cargas");
    }

    private static double GetProductionPdfColumnWidth(string column) => column switch
    {
        "#" => 40,
        "UNIDAD" => 70,
        "REM." => 70,
        "M3 X CARGA" => 80,
        "ACUMULADO" => 85,
        "TIPO DE CONCRETO" => 155,
        "N. REM." => 70,
        "CLIENTE" => 120,
        "SALIDA PLANTA" or "LLEGADA PLANTA" => 90,
        "ELEMENTO" => 110,
        "OBRA Y RESIDENTE" => 220,
        _ => 52
    };

    private static byte[] BuildFormulaPdf(ReportCatalogResult result)
    {
        const double titleTop = 32;
        const double tableTop = 94;
        const double headerHeight = 32;
        const double margin = 32;
        const double footerHeight = 32;
        const double fontSize = 7;
        var contentWidth = PdfA3LandscapeWidth - margin * 2;
        var columnWidths = GetFormulaPdfColumnWidths(result.Columns, contentWidth);
        var rowHeights = result.Rows
            .Select((row, index) => GetFormulaPdfRowHeight(result, row, columnWidths, fontSize))
            .ToArray();
        var availableRowsHeight = PdfA3LandscapeHeight - tableTop - headerHeight - margin - footerHeight;
        var pages = new List<(int Start, int End)>();
        var pageStart = 0;
        while (pageStart < result.Rows.Count)
        {
            var pageEnd = pageStart;
            var usedHeight = 0d;
            while (pageEnd < result.Rows.Count
                && (pageEnd == pageStart || usedHeight + rowHeights[pageEnd] <= availableRowsHeight))
            {
                usedHeight += rowHeights[pageEnd];
                pageEnd++;
            }

            pages.Add((pageStart, pageEnd));
            pageStart = pageEnd;
        }

        if (pages.Count == 0)
        {
            pages.Add((0, 0));
        }

        var pageContents = new List<byte[]>(pages.Count);
        for (var pageIndex = 0; pageIndex < pages.Count; pageIndex++)
        {
            var (start, end) = pages[pageIndex];
            var content = new StringBuilder("q\n");
            AppendPdfText(content, result.Definition.Name.ToUpperInvariant(), margin, titleTop, 15, bold: true, contentWidth);
            AppendPdfText(content, $"Fecha y hora de impresión: {DateTime.Now:dd/MM/yyyy, HH:mm}", margin, titleTop + 29, 8, bold: false, contentWidth);

            var x = margin;
            for (var columnIndex = 0; columnIndex < result.Columns.Count; columnIndex++)
            {
                var width = columnWidths[columnIndex];
                var y = PdfA3LandscapeHeight - tableTop - headerHeight;
                AppendPdfRectangle(content, x, y, width, headerHeight, "0.15 0.23 0.35");
                AppendPdfCellBorder(content, x, y, width, headerHeight);
                if (FormulaReportLayout.TryGetCategory(result.Columns[columnIndex], out var category))
                {
                    AppendPdfText(content, result.Columns[columnIndex].ToUpperInvariant(), x + 3, tableTop + 3, 7, bold: true, width - 6, white: true);
                    AppendPdfText(content, $"MATERIAL                 CANTIDAD ({category.Unit})", x + 3, tableTop + 17, 5.5, bold: false, width - 6, white: true);
                }
                else
                {
                    AppendPdfText(content, result.Columns[columnIndex], x + 4, tableTop + 10, 7, bold: true, width - 8, white: true);
                }

                x += width;
            }

            var rowTop = tableTop + headerHeight;
            for (var rowIndex = start; rowIndex < end; rowIndex++)
            {
                var row = result.Rows[rowIndex];
                var rowHeight = rowHeights[rowIndex];
                var fill = (rowIndex - start) % 2 == 0 ? "1 1 1" : "0.94 0.95 0.96";
                x = margin;
                for (var columnIndex = 0; columnIndex < result.Columns.Count; columnIndex++)
                {
                    var width = columnWidths[columnIndex];
                    var y = PdfA3LandscapeHeight - rowTop - rowHeight;
                    AppendPdfRectangle(content, x, y, width, rowHeight, fill);
                    AppendPdfCellBorder(content, x, y, width, rowHeight);
                    AppendFormulaPdfCell(content, result.Columns[columnIndex], row[columnIndex], x, rowTop, width, fontSize);
                    x += width;
                }

                rowTop += rowHeight;
            }

            var rowRange = result.Rows.Count == 0
                ? "Sin registros"
                : $"Fórmulas {start + 1}-{end} de {result.Rows.Count}";
            AppendPdfText(content, $"Página {pageIndex + 1} de {pages.Count} | {rowRange}", margin, PdfA3LandscapeHeight - margin - 12, 8, bold: false, contentWidth);
            content.Append("Q\n");
            pageContents.Add(Encoding.ASCII.GetBytes(content.ToString()));
        }

        return BuildPdfDocument(pageContents, result.Definition.Name);
    }

    private static double[] GetFormulaPdfColumnWidths(IReadOnlyList<string> columns, double contentWidth)
    {
        var fixedWidth = columns.Sum(column =>
            FormulaReportLayout.TryGetCategory(column, out _)
                ? 0
                : column.Contains("Descripción", StringComparison.OrdinalIgnoreCase) ? 150 : 90);
        var categoryCount = columns.Count(column => FormulaReportLayout.TryGetCategory(column, out _));
        var categoryWidth = categoryCount == 0
            ? 0
            : Math.Max(80, (contentWidth - fixedWidth) / categoryCount);
        var widths = columns.Select(column =>
            FormulaReportLayout.TryGetCategory(column, out _)
                ? categoryWidth
                : column.Contains("Descripción", StringComparison.OrdinalIgnoreCase) ? 150d : 90d).ToArray();
        var total = widths.Sum();
        if (total > contentWidth && categoryCount > 0)
        {
            var widthAdjustment = (total - contentWidth) / categoryCount;
            for (var index = 0; index < columns.Count; index++)
            {
                if (FormulaReportLayout.TryGetCategory(columns[index], out _))
                {
                    widths[index] -= widthAdjustment;
                }
            }
        }

        return widths;
    }

    private static double GetFormulaPdfRowHeight(
        ReportCatalogResult result,
        IReadOnlyList<string> row,
        IReadOnlyList<double> columnWidths,
        double fontSize)
    {
        var lineCount = 1;
        for (var index = 0; index < result.Columns.Count; index++)
        {
            if (FormulaReportLayout.TryGetCategory(result.Columns[index], out _))
            {
                lineCount = Math.Max(lineCount, FormulaReportLayout.ParseEntries(row[index]).Count);
            }
            else
            {
                lineCount = Math.Max(lineCount, WrapPdfText(row[index], columnWidths[index], fontSize).Count);
            }
        }

        return Math.Max(20, lineCount * 10 + 8);
    }

    private static void AppendFormulaPdfCell(
        StringBuilder content,
        string column,
        string value,
        double x,
        double top,
        double width,
        double fontSize)
    {
        if (FormulaReportLayout.TryGetCategory(column, out _))
        {
            var entries = FormulaReportLayout.ParseEntries(value);
            for (var index = 0; index < entries.Count; index++)
            {
                var lineTop = top + 4 + index * 10;
                AppendPdfText(content, entries[index].Material, x + 3, lineTop, fontSize, bold: false, width - 40);
                AppendPdfText(content, entries[index].Quantity, x + width - 36, lineTop, fontSize, bold: false, 33);
            }

            return;
        }

        var lines = WrapPdfText(value, width - 8, fontSize);
        for (var index = 0; index < lines.Count; index++)
        {
            AppendPdfText(content, lines[index], x + 4, top + 4 + index * 10, fontSize, bold: false, width - 8);
        }
    }

    private static IReadOnlyList<string> WrapPdfText(string value, double width, double fontSize)
    {
        var maximumCharacters = Math.Max(1, (int)(width / (fontSize * 0.55)));
        var lines = new List<string>();
        var currentLine = new StringBuilder();
        foreach (var word in value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (currentLine.Length > 0 && currentLine.Length + word.Length + 1 > maximumCharacters)
            {
                lines.Add(currentLine.ToString());
                currentLine.Clear();
            }

            if (currentLine.Length > 0)
            {
                currentLine.Append(' ');
            }

            currentLine.Append(word);
        }

        if (currentLine.Length > 0)
        {
            lines.Add(currentLine.ToString());
        }

        return lines.Count == 0 ? new[] { string.Empty } : lines;
    }

    private static ReportCatalogResult ExpandInventoryStockForPdf(ReportCatalogResult result)
    {
        var columns = new List<string>(result.Columns.Count * 2);
        foreach (var category in result.Columns)
        {
            var shortCategory = category.StartsWith("TOTAL ", StringComparison.OrdinalIgnoreCase)
                ? category[6..]
                : category;
            columns.Add($"{shortCategory} - Material");
            columns.Add($"{shortCategory} - Existencia");
        }

        var rows = result.Rows
            .Select(row =>
            {
                var expandedRow = new List<string>(result.Columns.Count * 2);
                foreach (var value in row)
                {
                    var (material, quantity) = SplitInventoryStockValue(value);
                    expandedRow.Add(material);
                    expandedRow.Add(quantity);
                }

                return (IReadOnlyList<string>)expandedRow;
            })
            .ToArray();
        return new ReportCatalogResult(result.Definition, columns, rows, result.IsTruncated);
    }

    private static (string Material, string Quantity) SplitInventoryStockValue(string value)
    {
        var separatorIndex = value.IndexOf('\t');
        return separatorIndex < 0
            ? (value, string.Empty)
            : (value[..separatorIndex], value[(separatorIndex + 1)..]);
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
