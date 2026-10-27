namespace Ranalo.Woocommece.Api.Services
{
    // The order sync re-reads changed WooCommerce orders every 5 minutes and
    // used to overwrite the deposit M-Pesa code with WooCommerce's every
    // time. Order 19928 was enrolled with its deposit found (UJPEOBI5XU) and
    // later held UJPEOB15XU, a code nobody paid, so no contract was made and
    // the phone would have relocked (2026-10-26). A code we've received a
    // payment for is now kept over one we haven't.
    public static class DepositCodeGuard
    {
        // The code to save on an order already in our database.
        public static string? CodeToSave(string? existing, string? incoming, bool existingReceived, bool incomingReceived)
        {
            if (Same(existing, incoming))
            {
                return incoming;
            }

            return existingReceived && !incomingReceived ? existing : incoming;
        }

        public static bool Same(string? a, string? b) =>
            string.Equals(a?.Trim() ?? "", b?.Trim() ?? "", StringComparison.OrdinalIgnoreCase);
    }
}
