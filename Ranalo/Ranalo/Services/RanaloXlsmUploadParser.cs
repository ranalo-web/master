using Ranalo.Models;
using System.ComponentModel;
using System.Globalization;
using OfficeOpenXml;
using ClosedXML.Excel;

namespace Ranalo.Services
{
    public static class RanaloXlsmUploadParser
    {
        public static List<PaymentTransaction> Parse(IFormFile file)
        {
            using var stream = new MemoryStream();
            file.CopyTo(stream);
            stream.Position = 0;

            using var workbook = new XLWorkbook(stream);
            var ws = workbook.Worksheet(1);

            var results = new List<PaymentTransaction>();
            int headerRow = FindHeaderRow(ws);

            for (int row = headerRow + 1; row <= ws.LastRowUsed().RowNumber(); row++)
            {
                var receipt = ws.Cell(row, 1).GetString();
                if (!receipt.StartsWith("UA"))
                    continue;

                var paidInText = ws.Cell(row, 6).GetString();
                if (string.IsNullOrWhiteSpace(paidInText))
                    continue;

                results.Add(new PaymentTransaction
                {
                    ReceiptNo = receipt,
                    CompletionTime = ws.Cell(row, 2).GetString(),
                    InitiationTime = ws.Cell(row, 3).GetString(),
                    Details = ws.Cell(row, 4).GetString(),
                    Status = ws.Cell(row, 5).GetString(),
                    PaidIn = decimal.Parse(paidInText),
                    Balance = ws.Cell(row, 8).GetValue<decimal>(),
                    BalanceConfirmed = ws.Cell(row, 9).GetString(),
                    Reason = ws.Cell(row, 10).GetString(),
                    OtherPartyInfo = ws.Cell(row, 11).GetString(),
                    AccountNumber = ws.Cell(row, 13).GetString(),
                    Currency = ws.Cell(row, 14).GetString()
                });
            }

            return results;
        }

        private static int FindHeaderRow(IXLWorksheet ws)
        {
            foreach (var row in ws.RowsUsed())
            {
                if (row.Cell(1).GetString().Contains("Receipt No"))
                    return row.RowNumber();
            }
            throw new InvalidOperationException("Header not found");
        }
    }
}
