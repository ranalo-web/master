using ClosedXML.Excel;
using Ranalo.Models;
using System.Globalization;
using System.Text.RegularExpressions;

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

        public static BankAccountStatement Parse(string rawText)
        {
            var statement = new BankAccountStatement();
            rawText = Regex.Replace(rawText, @"(\r\n|\r|\n)+", "\n").Trim(); // normalize newlines
            rawText = Regex.Replace(rawText, @"(.)\1{2,}", "$1"); // remove double letters like "AAccccoouunntt"
            rawText = Regex.Replace(rawText, @"\s+", " "); // normalize spaces

            // Extract header fields
            statement.AccountNumber = Regex.Match(rawText, @"Account Number\s+(\d+)").Groups[1].Value;
            statement.Currency = Regex.Match(rawText, @"Currency\s+([A-Z]+)").Groups[1].Value;
            statement.AccountName = Regex.Match(rawText, @"RANALO CREDIT LIMITED", RegexOptions.IgnoreCase).Success ? "RANALO CREDIT LIMITED" : null;
            statement.GenerationDateTime = TryParseDate(Regex.Match(rawText, @"Statement Date\s+(\d{2}/\d{2}/\d{4})").Groups[1].Value);

            // Period (e.g. 05/10/2023 - 05/10/2025)
            var periodMatch = Regex.Match(rawText, @"Statement\s+(\d{2}/\d{2}/\d{4})\s*-\s*(\d{2}/\d{2}/\d{4})");
            if (periodMatch.Success)
            {
                statement.PeriodStart = TryParseDate(periodMatch.Groups[1].Value);
                statement.PeriodEnd = TryParseDate(periodMatch.Groups[2].Value);
            }

            return statement;
        }
        private static DateTime? TryParseDate(string date)
        {
            if (DateTime.TryParseExact(date, "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var result))
                return result;
            return null;
        }

        public static BankAccountStatement MapFromPdf(string rawText)
        {
            var statement = new BankAccountStatement
            {
                AccountNumber = ExtractAccountNumber(rawText),
                Currency = ExtractCurrency(rawText),
            };

            (statement.PeriodStart, statement.PeriodEnd) = ExtractPeriod(rawText);
            statement.Transactions = ExtractTransactions(rawText);

            return statement;
        }

        private static string ExtractAccountNumber(string text)
        {
            var match = Regex.Match(text, @"Account\s*Number\s*(\d+)", RegexOptions.IgnoreCase);
            return match.Success ? match.Groups[1].Value : string.Empty;
        }

        private static string ExtractCurrency(string text)
        {
            var match = Regex.Match(text, @"Currency\s+([A-Z]{3})", RegexOptions.IgnoreCase);
            return match.Success ? match.Groups[1].Value : string.Empty;
        }

        private static (DateTime?, DateTime?) ExtractPeriod(string text)
        {
            var match = Regex.Match(text, @"Statement\s+(\d{2}/\d{2}/\d{4})\s*-\s*(\d{2}/\d{2}/\d{4})", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                DateTime.TryParse(match.Groups[1].Value, out var start);
                DateTime.TryParse(match.Groups[2].Value, out var end);
                return (start, end);
            }
            return (null, null);
        }

        public static List<BankTransaction> ExtractTransactions(string raw)
        {
            // 🧹 Clean and normalize text
            raw = Regex.Replace(raw, @"(\w)\1{2,}", "$1");       // fix OCR duplicated letters
            raw = Regex.Replace(raw, @"[^\w\s/.,]", " ");        // remove stray non-word symbols
            raw = Regex.Replace(raw, @"\s+", " ");               // normalize spacing

            // 🧩 Match both S-codes and numeric references:
            // Example matches:
            //   S8040806 29/09/2024 57,513.0 8,639.84
            //   54300795 11/09/2024 100.00 1,900.00
            var pattern = @"(?:(S\d{3,})|(\d{6,}))\s+\d{1,2}/\d{1,2}/\d{4}\s+([\d,]+\.?\d*)\s+([\d,]+\.?\d*)";

            var matches = Regex.Matches(raw, pattern);
            var txns = new List<BankTransaction>();

            foreach (Match m in matches)
            {
                string refValue = !string.IsNullOrWhiteSpace(m.Groups[1].Value)
                    ? m.Groups[1].Value
                    : m.Groups[2].Value;

                var dateMatch = Regex.Match(m.Value, @"\d{1,2}/\d{1,2}/\d{4}");

                if (!dateMatch.Success)
                    continue;

                var txn = new BankTransaction
                {
                    BankReference = refValue,
                    ValueDate = DateTime.TryParse(dateMatch.Value, out var d) ? d : null,
                    CreditAmount = ParseDecimal(m.Groups[3].Value),
                    DebitAmount = ParseDecimal(m.Groups[4].Value),
                };

                // 🧾 Try to find a running balance just after this match
                var balanceMatch = Regex.Match(raw.Substring(m.Index + m.Length), @"([\d,]+\.?\d*)");
                if (balanceMatch.Success)
                    txn.RunningBalance = ParseDecimal(balanceMatch.Groups[1].Value);

                // 🔍 Capture transaction description (text right before reference)
                var start = Math.Max(0, m.Index - 80);
                var prefix = raw.Substring(start, m.Index - start);
                var detailMatch = Regex.Match(prefix, @"([A-Z0-9/ +]{4,})$", RegexOptions.IgnoreCase);
                txn.TransactionDetails = detailMatch.Success ? detailMatch.Groups[1].Value.Trim() : "";

                txns.Add(txn);
            }

            return txns;
        }

        private static decimal? ParseDecimal(string s)
        {
            if (decimal.TryParse(s.Replace(",", ""), out var d))
                return d;
            return null;
        }
    }
}
