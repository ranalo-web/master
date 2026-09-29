using System.Globalization;
using ClosedXML.Excel;
using Ranalo.Models;

namespace Ranalo.Services
{
    // Builds the bulk Operating Expenses template and reads it back. The
    // columns match the single "Log an expense" form: Date, Category,
    // Description, Amount. Rows are validated here but never saved -- the
    // review screen lets the user fix them and assign categories first.
    public static class OperatingExpenseUploadParser
    {
        public const int MaxRows = 1000;

        private static readonly string[] Headers = { "Date", "Category", "Description", "Amount" };

        private static readonly string[] DateFormats =
        {
            "yyyy-MM-dd", "dd/MM/yyyy", "d/M/yyyy", "dd-MM-yyyy", "d-M-yyyy", "dd MMM yyyy", "d MMM yyyy",
        };

        public static byte[] BuildTemplate()
        {
            using var workbook = new XLWorkbook();

            // Expenses first so it opens as the active sheet.
            var ws = workbook.AddWorksheet("Expenses");
            var lists = workbook.AddWorksheet("Categories");
            lists.Cell(1, 1).Value = "Category";
            lists.Cell(1, 1).Style.Font.Bold = true;
            for (var i = 0; i < OperatingExpenseCategory.All.Count; i++)
            {
                lists.Cell(i + 2, 1).Value = OperatingExpenseCategory.All[i];
            }
            var categoryRange = lists.Range(2, 1, OperatingExpenseCategory.All.Count + 1, 1);

            for (var c = 0; c < Headers.Length; c++)
            {
                ws.Cell(1, c + 1).Value = Headers[c];
            }
            var header = ws.Range(1, 1, 1, Headers.Length);
            header.Style.Font.Bold = true;
            header.Style.Fill.BackgroundColor = XLColor.LightGray;

            ws.Column(1).Style.NumberFormat.Format = "yyyy-mm-dd";
            ws.Column(4).Style.NumberFormat.Format = "#,##0.00";
            ws.Range(2, 2, MaxRows + 1, 2).CreateDataValidation().List(categoryRange);
            ws.Column(1).Width = 14;
            ws.Column(2).Width = 16;
            ws.Column(3).Width = 50;
            ws.Column(4).Width = 14;
            ws.SheetView.FreezeRows(1);

            var notes = workbook.AddWorksheet("Instructions");
            var lines = new[]
            {
                "How to fill in the Expenses sheet",
                "",
                "Date: the date the expense was incurred, e.g. 2024-03-31 (dd/MM/yyyy also works).",
                "Category: optional -- pick from the drop-down (" + string.Join(", ", OperatingExpenseCategory.All) + "). Blank categories are assigned on the review screen after upload.",
                "Description: optional, up to 300 characters, e.g. \"March payroll\".",
                "Amount: in KES, greater than 0. Numbers only, no currency symbol.",
                "",
                "One expense per row, starting on row 2. Keep the header row. Up to " + MaxRows + " rows per upload.",
                "Nothing is saved until you review the rows and click Save on the review screen.",
            };
            for (var i = 0; i < lines.Length; i++)
            {
                notes.Cell(i + 1, 1).Value = lines[i];
            }
            notes.Cell(1, 1).Style.Font.Bold = true;
            notes.Column(1).Width = 110;

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return stream.ToArray();
        }

        public static List<OperatingExpenseUploadRow> Parse(IFormFile file)
        {
            using var stream = new MemoryStream();
            file.CopyTo(stream);
            stream.Position = 0;

            using var workbook = new XLWorkbook(stream);
            var ws = workbook.Worksheets.FirstOrDefault(w => w.Name.Equals("Expenses", StringComparison.OrdinalIgnoreCase))
                     ?? workbook.Worksheet(1);

            var headerRow = ws.RowsUsed()
                .FirstOrDefault(r => r.Cell(1).GetString().Trim().Equals("Date", StringComparison.OrdinalIgnoreCase))
                ?.RowNumber()
                ?? throw new InvalidOperationException("Header row not found -- the first column must be headed \"Date\". Download the template and use that layout.");

            var lastRow = ws.LastRowUsed()?.RowNumber() ?? headerRow;
            var rows = new List<OperatingExpenseUploadRow>();

            for (var r = headerRow + 1; r <= lastRow; r++)
            {
                var dateCell = ws.Cell(r, 1);
                var categoryText = ws.Cell(r, 2).GetString().Trim();
                var description = ws.Cell(r, 3).GetString().Trim();
                var amountCell = ws.Cell(r, 4);

                if (dateCell.IsEmpty() && categoryText == "" && description == "" && amountCell.IsEmpty())
                {
                    continue;
                }

                if (rows.Count >= MaxRows)
                {
                    throw new InvalidOperationException($"The file has more than {MaxRows} expense rows. Split it into smaller uploads.");
                }

                var row = new OperatingExpenseUploadRow
                {
                    RowNumber = r,
                    Description = description.Length > 300 ? description[..300] : description,
                    ExpenseDate = ReadDate(dateCell),
                    Amount = ReadAmount(amountCell),
                };

                if (categoryText != "")
                {
                    row.Category = OperatingExpenseCategory.All
                        .FirstOrDefault(c => c.Equals(categoryText, StringComparison.OrdinalIgnoreCase));
                    if (row.Category == null)
                    {
                        row.Errors.Add($"Unknown category '{categoryText}' -- choose one.");
                    }
                }

                Validate(row);
                rows.Add(row);
            }

            return rows;
        }

        // Shared by Parse and the save step, so a row edited on the review
        // screen is checked by the same rules. Category is only required
        // at save time (requireCategory), since it may be assigned later.
        public static void Validate(OperatingExpenseUploadRow row, bool requireCategory = false)
        {
            if (row.ExpenseDate == null)
            {
                row.Errors.Add("Missing or unreadable date.");
            }
            else if (row.ExpenseDate.Value.Date > DateTime.Now.Date)
            {
                row.Errors.Add("Date is in the future.");
            }

            if (row.Amount == null || row.Amount <= 0)
            {
                row.Errors.Add("Amount must be a number greater than 0.");
            }

            if (requireCategory && (row.Category == null || !OperatingExpenseCategory.All.Contains(row.Category)))
            {
                row.Errors.Add("Choose a category.");
            }
        }

        private static DateTime? ReadDate(IXLCell cell)
        {
            if (cell.IsEmpty())
            {
                return null;
            }

            if (cell.DataType == XLDataType.DateTime)
            {
                return cell.GetDateTime().Date;
            }

            if (cell.DataType == XLDataType.Number)
            {
                // A date typed into a cell Excel didn't format as a date.
                var serial = cell.GetDouble();
                return serial > 0 && serial < 2958466 ? DateTime.FromOADate(serial).Date : null;
            }

            var text = cell.GetString().Trim();
            return DateTime.TryParseExact(text, DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
                ? date.Date
                : null;
        }

        private static decimal? ReadAmount(IXLCell cell)
        {
            if (cell.IsEmpty())
            {
                return null;
            }

            if (cell.DataType == XLDataType.Number)
            {
                return Math.Round((decimal)cell.GetDouble(), 2);
            }

            var text = cell.GetString().Trim()
                .Replace("KES", "", StringComparison.OrdinalIgnoreCase)
                .Replace("KSh", "", StringComparison.OrdinalIgnoreCase)
                .Replace(",", "")
                .Trim();
            return decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var amount)
                ? Math.Round(amount, 2)
                : null;
        }
    }
}
