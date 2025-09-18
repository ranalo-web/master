using ClosedXML.Excel;
using Ranalo.Models;

namespace Ranalo.Controllers
{
    public class BankStatementMapper
    {

        public BankAccountStatement MapFromExcel(Stream excelStream, long dealerId, string fileName)
        {
            using var workbook = new XLWorkbook(excelStream);
            var ws = workbook.Worksheet(1);

            var statement = new BankAccountStatement();

            // --- Parse summary (assuming fixed positions like in your screenshot) ---
            statement.AccountName = ws.Cell("B2").GetString();           // Account Name
            statement.AccountNumber = ws.Cell("B3").GetString();         // Account Number
            statement.AccountType = ws.Cell("B4").GetString();           // Account Type
            statement.DealerId = dealerId;
            statement.FileName = fileName;

            // Parse Period: "01/09/2024 to 31/08/2025"
            var periodText = ws.Cell("B6").GetString();
            if (!string.IsNullOrWhiteSpace(periodText) && periodText.Contains("to"))
            {
                var parts = periodText.Split("to", StringSplitOptions.TrimEntries);
                if (DateTime.TryParse(parts[0], out var start)) statement.PeriodStart = start;
                if (DateTime.TryParse(parts[1], out var end)) statement.PeriodEnd = end;
            }

            statement.GeneratedBy = ws.Cell("B7").GetString();
            statement.AvailableBalance = ws.Cell("E2").GetValue<decimal?>();
            statement.BalanceAtPeriodStart = ws.Cell("E3").GetValue<decimal?>();
            statement.BalanceAtPeriodEnd = ws.Cell("E4").GetValue<decimal?>();
            statement.TotalCredits = ws.Cell("E5").GetValue<decimal?>();
            statement.TotalDebits = ws.Cell("E6").GetValue<decimal?>();
            statement.Currency = ws.Cell("E7").GetString();

            // --- Parse transactions (starting at row 8 in your screenshot) ---
            var transactions = new List<BankTransaction>();
            int row = 9; // adjust depending on actual header row
            while (!ws.Cell(row, 1).IsEmpty())
            {
                var tx = new BankTransaction
                {
                    PostingDate = ws.Cell(row, 1).GetDateTime(),
                    ValueDate = ws.Cell(row, 2).GetDateTime(),
                    BankReference = ws.Cell(row, 3).GetString(),
                    ChannelReference = ws.Cell(row, 4).GetString(),
                    TransactionType = ws.Cell(row, 5).GetString(),
                    TransactionDetails = ws.Cell(row, 6).GetString(),
                    DebitAmount = ws.Cell(row, 7).IsEmpty() ? null : ws.Cell(row, 7).GetValue<decimal?>(),
                    CreditAmount = ws.Cell(row, 8).IsEmpty() ? null : ws.Cell(row, 8).GetValue<decimal?>(),
                    RunningBalance = ws.Cell(row, 9).IsEmpty() ? null : ws.Cell(row, 9).GetValue<decimal?>()
                };

                transactions.Add(tx);
                row++;
            }

            statement.Transactions = transactions;

            return statement;
        }
    }
}
