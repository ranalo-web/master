namespace Ranalo.Services
{
    // The single commission formula for the whole app. Every commission
    // figure (dashboard cards, dashboard Commissions section, Account
    // Commissions page, and the admin Commissions pages) goes through
    // Calculate below, so changing a rate or rule here changes it everywhere.
    //
    // Agent commission
    //   Upfront = 50% of the deposit, from the contract start.
    //   Bonus   = 25% of the deposit, only once the contract is 90+ days old
    //             AND the account is performing (not past its lock date)
    //             AND the contract has a WooCommerce order (Woo_Orders.ContractId).
    //             Otherwise it is held, and paid once the account qualifies.
    //   Arrears deducted = the account's whole shortfall (amount due to date
    //             minus total paid -- the Balance on Account Details) whenever
    //             it is behind, whatever its lock date, written off or not.
    //             Lock dates are not a reliable sign of being behind: badly
    //             behind accounts can still show a lock date in the future.
    //   Net     = upfront + bonus - arrears deducted - paid to the agent.
    //
    // Dealer commission
    //   Base    = total paid - buying price - agent commission (upfront + bonus).
    //             The agent commission is subtracted even on direct sales with no
    //             agent, so unassigning an agent can never inflate the dealer's share.
    //   Commission = 30% of the base, never below 0. It grows with every payment.
    //   Arrears deducted = the same shortfall as above.
    //   Net     = commission - arrears deducted; Balance = net - paid to the dealer.
    //   No buying price recorded: no dealer commission at all until it is entered.
    public static class CommissionCalculator
    {
        public const decimal AgentUpfrontRate = 0.50m;
        public const decimal AgentBonusRate = 0.25m;
        public const decimal DealerRate = 0.30m;
        public const int BonusDays = 90;

        public static CommissionBreakdown Calculate(CommissionInputs i)
        {
            var trueArrears = i.Arrears < 0 ? -i.Arrears : 0;
            var bonusDue = i.DaysSinceStart >= BonusDays;
            var bonusEarned = bonusDue && !i.IsPastLockDate && i.HasWooOrder;
            var upfront = i.Deposit * AgentUpfrontRate;
            var bonus = bonusEarned ? i.Deposit * AgentBonusRate : 0;
            var agentCommission = upfront + bonus;

            var agentDeducted = i.HasAgent ? trueArrears : 0;
            var agentEarned = i.HasAgent ? agentCommission : 0;

            decimal? dealerBase = null, dealerCommission = null, dealerNet = null, dealerBalance = null;
            decimal dealerDeducted = 0;
            if (i.BuyingPrice.HasValue)
            {
                dealerBase = i.TotalPaid - i.BuyingPrice.Value - agentCommission;
                dealerCommission = Math.Max(0, dealerBase.Value) * DealerRate;
                dealerDeducted = trueArrears;
                dealerNet = dealerCommission.Value - dealerDeducted;
                dealerBalance = dealerNet.Value - i.DealerPaid;
            }

            return new CommissionBreakdown
            {
                IsPastLockDate = i.IsPastLockDate,
                HasWooOrder = i.HasWooOrder,
                TrueArrears = trueArrears,
                AgentUpfront = upfront,
                AgentBonus = bonus,
                BonusEarned = bonusEarned,
                BonusAtRisk = bonusDue && !bonusEarned,
                BonusHeldNoWooOrder = bonusDue && !i.HasWooOrder,
                DaysToBonus = Math.Max(0, BonusDays - i.DaysSinceStart),
                AgentCommission = agentCommission,
                AgentEarned = agentEarned,
                AgentArrearsDeducted = agentDeducted,
                AgentPaid = i.AgentPaid,
                AgentNet = i.HasAgent ? agentEarned - agentDeducted - i.AgentPaid : 0,
                DealerBase = dealerBase,
                DealerCommission = dealerCommission,
                DealerArrearsDeducted = dealerDeducted,
                DealerPaid = i.DealerPaid,
                DealerNet = dealerNet,
                DealerBalance = dealerBalance,
            };
        }

        // Totals for one agent's or one dealer's accounts. Arrears on one
        // account reduce what is owed on the others; Withheld is capped at
        // what was earned and Owed is floored at 0 for the group as a whole.
        public static CommissionPool Pool(IEnumerable<(decimal Earned, decimal Deducted, decimal Paid)> accounts)
        {
            decimal earned = 0, deducted = 0, paid = 0;
            foreach (var a in accounts)
            {
                earned += a.Earned;
                deducted += a.Deducted;
                paid += a.Paid;
            }

            return new CommissionPool
            {
                Earned = earned,
                Deducted = deducted,
                Withheld = Math.Min(deducted, earned),
                Paid = paid,
                Owed = Math.Max(0, earned - deducted - paid),
            };
        }

        public static CommissionPool PoolAgent(IEnumerable<CommissionBreakdown> accounts) =>
            Pool(accounts.Select(b => (b.AgentEarned, b.AgentArrearsDeducted, b.AgentPaid)));

        public static CommissionPool PoolDealer(IEnumerable<CommissionBreakdown> accounts) =>
            Pool(accounts.Select(b => (b.DealerCommission ?? 0, b.DealerArrearsDeducted, b.DealerPaid)));
    }

    public class CommissionInputs
    {
        public decimal Deposit { get; set; }
        public int DaysSinceStart { get; set; }
        public bool IsPastLockDate { get; set; }

        // Total paid minus amount due to date; negative means a shortfall.
        public decimal Arrears { get; set; }
        public decimal TotalPaid { get; set; }
        public decimal? BuyingPrice { get; set; }
        public bool HasAgent { get; set; }
        public decimal AgentPaid { get; set; }
        public decimal DealerPaid { get; set; }

        // The contract has a WooCommerce order. Without one the bonus is held.
        public bool HasWooOrder { get; set; }
    }

    public class CommissionBreakdown
    {
        public bool IsPastLockDate { get; set; }
        public bool HasWooOrder { get; set; }

        // The customer's shortfall; all of it is deducted.
        public decimal TrueArrears { get; set; }
        public bool IsBehind => TrueArrears > 0;

        public decimal AgentUpfront { get; set; }
        public decimal AgentBonus { get; set; }
        public bool BonusEarned { get; set; }
        // 90+ days old but the bonus is held: past the lock date and/or no
        // WooCommerce order (BonusHeldNoWooOrder says which).
        public bool BonusAtRisk { get; set; }
        public bool BonusHeldNoWooOrder { get; set; }
        public int DaysToBonus { get; set; }

        // Upfront + bonus at the standard rate, whether or not an agent is
        // assigned. Used as a cost in the dealer base.
        public decimal AgentCommission { get; set; }

        // Agent side; all 0 on a direct sale with no agent.
        public decimal AgentEarned { get; set; }
        public decimal AgentArrearsDeducted { get; set; }
        public decimal AgentPaid { get; set; }
        public decimal AgentNet { get; set; }

        // Dealer side; the nullable values are null when no buying price is recorded.
        public decimal? DealerBase { get; set; }
        public decimal? DealerCommission { get; set; }
        public decimal DealerArrearsDeducted { get; set; }
        public decimal DealerPaid { get; set; }
        public decimal? DealerNet { get; set; }
        public decimal? DealerBalance { get; set; }
    }

    public class CommissionPool
    {
        public decimal Earned { get; set; }
        public decimal Deducted { get; set; }
        public decimal Withheld { get; set; }
        public decimal Paid { get; set; }
        public decimal Owed { get; set; }
    }
}
