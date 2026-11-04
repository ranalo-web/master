namespace Ranalo.DataStore
{
    // One account's payments as a derived table: the ones paid straight to
    // it plus the ones re-pointed to it in OrphanedPayments. Same rows as
    //
    //   KosePayments kp LEFT JOIN OrphanedPayments op ON op.MpesaCode = kp.MpesaCode
    //   WHERE COALESCE(op.AccountNoBigint, kp.AccountNoBigint) = <account>
    //
    // but that COALESCE can't use IX_KosePayments_AccountDate, so it read
    // all 77k payments for every account it was asked about. Split in two,
    // each half seeks (2026-11-04: Restructured page 15 s -> 11 ms).
    //
    // Columns: Id, MpesaCode, Amount, AmountValue, PaymentDate,
    // PaymentDateValue, AccountNo. `account` is a parameter (@AccountNo) or
    // an outer column (d.Id); it's SQL text, never user input.
    public static class AccountPaymentsSql
    {
        public static string For(string account) => $@"(
                    SELECT kp.Id, kp.MpesaCode, kp.Amount, kp.AmountValue, kp.PaymentDate, kp.PaymentDateValue,
                           kp.AccountNoBigint AS AccountNo
                    FROM KosePayments kp
                    LEFT JOIN OrphanedPayments op ON op.MpesaCode = kp.MpesaCode
                    WHERE kp.AccountNoBigint = {account} AND op.AccountNoBigint IS NULL
                    UNION ALL
                    SELECT kp.Id, kp.MpesaCode, kp.Amount, kp.AmountValue, kp.PaymentDate, kp.PaymentDateValue,
                           op.AccountNoBigint AS AccountNo
                    FROM OrphanedPayments op
                    JOIN KosePayments kp ON kp.MpesaCode = op.MpesaCode
                    WHERE op.AccountNoBigint = {account})";
    }
}
