using ClosedXML.Excel;
using Microsoft.AspNetCore.Http;
using Ranalo.Models;
using Ranalo.Services;

namespace Ranolo.Web.Tests
{
    public class OperatingExpenseUploadParserTests
    {
        private static IFormFile ToFormFile(byte[] bytes) =>
            new FormFile(new MemoryStream(bytes), 0, bytes.Length, "file", "expenses.xlsx");

        // Round-trip: fill in the downloaded template the way a user would
        // in Excel, then parse it back.
        private static byte[] FillTemplate(Action<IXLWorksheet> fill)
        {
            using var workbook = new XLWorkbook(new MemoryStream(OperatingExpenseUploadParser.BuildTemplate()));
            fill(workbook.Worksheet("Expenses"));
            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return stream.ToArray();
        }

        [Test]
        public void Template_HasExpensesSheetWithFormHeaders()
        {
            using var workbook = new XLWorkbook(new MemoryStream(OperatingExpenseUploadParser.BuildTemplate()));
            var ws = workbook.Worksheet(1);

            Assert.That(ws.Name, Is.EqualTo("Expenses"));
            Assert.That(Enumerable.Range(1, 4).Select(c => ws.Cell(1, c).GetString()),
                Is.EqualTo(new[] { "Date", "Category", "Description", "Amount" }));
        }

        [Test]
        public void Parse_ReadsRowsAndLeavesBlankCategoryForReview()
        {
            var bytes = FillTemplate(ws =>
            {
                ws.Cell(2, 1).Value = new DateTime(2024, 3, 31);
                ws.Cell(2, 2).Value = "salaries";
                ws.Cell(2, 3).Value = "March payroll";
                ws.Cell(2, 4).Value = 150000;

                ws.Cell(3, 1).Value = "15/02/2024";
                ws.Cell(3, 3).Value = "Facebook ads";
                ws.Cell(3, 4).Value = "KES 12,500.50";
            });

            var rows = OperatingExpenseUploadParser.Parse(ToFormFile(bytes));

            Assert.That(rows, Has.Count.EqualTo(2));
            Assert.That(rows[0].ExpenseDate, Is.EqualTo(new DateTime(2024, 3, 31)));
            Assert.That(rows[0].Category, Is.EqualTo(OperatingExpenseCategory.Salaries));
            Assert.That(rows[0].Amount, Is.EqualTo(150000m));
            Assert.That(rows[0].Errors, Is.Empty);

            Assert.That(rows[1].ExpenseDate, Is.EqualTo(new DateTime(2024, 2, 15)));
            Assert.That(rows[1].Category, Is.Null);
            Assert.That(rows[1].Amount, Is.EqualTo(12500.50m));
            Assert.That(rows[1].Errors, Is.Empty);
        }

        [Test]
        public void Parse_FlagsBadRowsWithoutThrowing()
        {
            var bytes = FillTemplate(ws =>
            {
                ws.Cell(2, 1).Value = "not a date";
                ws.Cell(2, 2).Value = "Travel";
                ws.Cell(2, 4).Value = -5;
            });

            var row = OperatingExpenseUploadParser.Parse(ToFormFile(bytes)).Single();

            Assert.That(row.Errors, Has.Some.Contains("date"));
            Assert.That(row.Errors, Has.Some.Contains("Unknown category 'Travel'"));
            Assert.That(row.Errors, Has.Some.Contains("Amount"));
        }

        [Test]
        public void Validate_RequiresCategoryAtSave()
        {
            var row = new OperatingExpenseUploadRow { ExpenseDate = new DateTime(2024, 1, 5), Amount = 100m };

            OperatingExpenseUploadParser.Validate(row, requireCategory: true);

            Assert.That(row.Errors, Is.EqualTo(new[] { "Choose a category." }));
        }
    }
}
