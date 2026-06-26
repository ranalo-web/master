using Dapper;
using Ranalo.Calculator.Logic.Models;
using Ranalo.Models;
using Ranalo.Models.Reports;
using System.Data;

namespace Ranalo.DataStore
{
    public class CommissionsRepository : ICommissionsRepository
    {

        private readonly IDbConnection _db;

        public CommissionsRepository(IDbConnection db)
        {
            _db = db;
        }

        public async Task<PagedResult<MainCommissionsSummaryReport>>
    FullCommissionsReport(CommissionsFilter filter)
        {
            var sql = @"
                        WITH PaymentsSummary AS
                        (
                            SELECT
                                kp.AccountNoBigint,

                                SUM(ISNULL(kp.AmountValue, kp.Amount))
                                    AS TotalPaid,

                                MAX(kp.PaymentDateValue)
                                    AS LastPaymentDate

                            FROM dbo.KosePayments kp

                            GROUP BY kp.AccountNoBigint
                        ),

                        DealerPaymentsSummary AS
                        (
                            SELECT
                                dcp.ContractId,

                                SUM(ISNULL(dcp.AmountPaid, 0))
                                    AS TotalDealerPaid

                            FROM dbo.DealerCommissionPayments dcp

                            GROUP BY dcp.ContractId
                        )

                        SELECT
                            c.ContractID,

                            c.ID AS AccountNo,

                            c.First_Name,

                            c.StartDate,

                            c.Created,

                            ISNULL(c.Deposit, 0)
                                AS Deposit,

                            c.Total_Cost
                                AS TotalAmount,

                            ISNULL(wo.DeviceAmount, 0)
                                AS DeviceAmount,

                            ISNULL(p.TotalPaid, 0)
                                AS TotalPaid,

                            (
                                ISNULL(wo.DeviceAmount, 0)
                                + (ISNULL(c.Deposit, 0) * 0.75)
                                + 2000.00
                            ) AS DealerThreshold,

                            (
                                ISNULL(c.Deposit, 0) * 0.75
                            ) AS AgentCommission,

                            (
                                ISNULL(c.Total_Cost, 0) * 0.30
                            ) AS DealerCommission,



                            -- Earned dealer commission
                            CASE
                                WHEN
                                    ISNULL(p.TotalPaid, 0)
                                    >=
                                    (
                                        ISNULL(wo.DeviceAmount, 0)
                                        + (ISNULL(c.Deposit, 0) * 0.75)
                                        + 2000.00
                                    )

                                THEN
                                    (
                                        ISNULL(c.Total_Cost, 0) * 0.30
                                    )

                                ELSE
                                    (
                                        (
                                            ISNULL(p.TotalPaid, 0)
                                            /
                                            NULLIF
                                            (
                                                (
                                                    ISNULL(wo.DeviceAmount, 0)
                                                    + (ISNULL(c.Deposit, 0) * 0.75)
                                                    + 2000.00
                                                ),
                                                0
                                            )
                                        )
                                        *
                                        (
                                            ISNULL(c.Total_Cost, 0) * 0.30
                                        )
                                    )
                            END AS EarnedDealerCommission,



                            -- Already paid to dealer
                            ISNULL(dp.TotalDealerPaid, 0)
                                AS TotalDealerPaid,



                            -- Remaining payable to dealer
                            (
                                CASE
                                    WHEN
                                        ISNULL(p.TotalPaid, 0)
                                        >=
                                        (
                                            ISNULL(wo.DeviceAmount, 0)
                                            + (ISNULL(c.Deposit, 0) * 0.75)
                                            + 2000.00
                                        )

                                    THEN
                                        (
                                            ISNULL(c.Total_Cost, 0) * 0.30
                                        )

                                    ELSE
                                        (
                                            (
                                                ISNULL(p.TotalPaid, 0)
                                                /
                                                NULLIF
                                                (
                                                    (
                                                        ISNULL(wo.DeviceAmount, 0)
                                                        + (ISNULL(c.Deposit, 0) * 0.75)
                                                        + 2000.00
                                                    ),
                                                    0
                                                )
                                            )
                                            *
                                            (
                                                ISNULL(c.Total_Cost, 0) * 0.30
                                            )
                                        )
                                END
                                -
                                ISNULL(dp.TotalDealerPaid, 0)
                            ) AS RemainingDealerBalance,



                            -- Eligibility
                            CASE
                                WHEN
                                    ISNULL(p.TotalPaid, 0)
                                    >=
                                    (
                                        ISNULL(wo.DeviceAmount, 0)
                                        + (ISNULL(c.Deposit, 0) * 0.75)
                                        + 2000.00
                                    )

                                THEN CAST(1 AS BIT)

                                ELSE CAST(0 AS BIT)

                            END AS DealerEligible,



                            -- Payment status
                            CASE
                                WHEN
                                    ISNULL(dp.TotalDealerPaid, 0)
                                    >=
                                    (
                                        ISNULL(c.Total_Cost, 0) * 0.30
                                    )

                                THEN 'FULLY PAID'

                                WHEN
                                    ISNULL(dp.TotalDealerPaid, 0) > 0

                                THEN 'PARTIALLY PAID'

                                WHEN
                                    (
                                        CASE
                                            WHEN
                                                ISNULL(p.TotalPaid, 0)
                                                >=
                                                (
                                                    ISNULL(wo.DeviceAmount, 0)
                                                    + (ISNULL(c.Deposit, 0) * 0.75)
                                                    + 2000.00
                                                )

                                            THEN
                                                (
                                                    ISNULL(c.Total_Cost, 0) * 0.30
                                                )

                                            ELSE
                                                (
                                                    (
                                                        ISNULL(p.TotalPaid, 0)
                                                        /
                                                        NULLIF
                                                        (
                                                            (
                                                                ISNULL(wo.DeviceAmount, 0)
                                                                + (ISNULL(c.Deposit, 0) * 0.75)
                                                                + 2000.00
                                                            ),
                                                            0
                                                        )
                                                    )
                                                    *
                                                    (
                                                        ISNULL(c.Total_Cost, 0) * 0.30
                                                    )
                                                )
                                        END
                                        -
                                        ISNULL(dp.TotalDealerPaid, 0)
                                    ) > 0

                                THEN 'READY TO PAY'

                                ELSE 'NOT ELIGIBLE'

                            END AS PaymentStatus,



                            p.LastPaymentDate,

                            d.Name AS DeviceName,

                            d.CustomerPhoneNumber,

                            d.DeviceGroupId

                        INTO #CommissionReport

                        FROM dbo.Contract_Info c

                        LEFT JOIN PaymentsSummary p
                            ON p.AccountNoBigint = c.ID

                        LEFT JOIN dbo.Devices d
                            ON d.Id = c.ID

                        INNER JOIN dbo.Woo_Orders wo
                            ON wo.ContractId = c.ContractID

                        LEFT JOIN DealerPaymentsSummary dp
                            ON dp.ContractId = c.ContractID

                        WHERE
                            c.ContractID IS NOT NULL;



                        SELECT *
                        FROM #CommissionReport
                        WHERE
                            (
                                @DealerId IS NULL
                                OR DeviceGroupId = @DealerId
                            )
                            AND
                            (
                                @DealerEligible IS NULL
                                OR DealerEligible = @DealerEligible
                            )

                        ORDER BY Created DESC

                        OFFSET (@PageNumber - 1) * @PageSize ROWS
                        FETCH NEXT @PageSize ROWS ONLY;



                        SELECT COUNT(*)
                        FROM #CommissionReport
                        WHERE
                            (
                                @DealerId IS NULL
                                OR DeviceGroupId = @DealerId
                            )
                            AND
                            (
                                @DealerEligible IS NULL
                                OR DealerEligible = @DealerEligible
                            );



                        DROP TABLE #CommissionReport;
                        ";

            var parameters = new
            {
                filter.DealerId,
                filter.DealerEligible,
                filter.PageNumber,
                filter.PageSize
            };

            using var multi =
                await _db.QueryMultipleAsync(sql, parameters);

            var items =
                await multi.ReadAsync<MainCommissionsSummaryReport>();

            var totalCount =
                await multi.ReadFirstAsync<int>();

            return new PagedResult<MainCommissionsSummaryReport>
            {
                Items = items,
                TotalCount = totalCount,
                PageNumber = filter.PageNumber,
                PageSize = filter.PageSize
            };
        }

        // Create
        public async Task<PagedResult<OutstandingDealerCommissionReport>>
OutstandingDealerCommissions(
    CommissionsFilter filter)
        {
            var sql = @"

                    WITH PaymentsSummary AS
                    (
                        SELECT
                            AccountNoBigint,

                            SUM(ISNULL(AmountValue, Amount))
                                AS TotalPaid

                        FROM dbo.KosePayments

                        GROUP BY AccountNoBigint
                    ),

                    DealerPaymentsSummary AS
                    (
                        SELECT
                            ContractId,

                            SUM(ISNULL(AmountPaid, 0))
                                AS TotalDealerPaid

                        FROM dbo.DealerCommissionPayments

                        GROUP BY ContractId
                    )

                    SELECT
                        c.ContractID,

                        c.ID AS AccountNo,

                        c.First_Name,

                        c.Total_Cost AS TotalAmount,

                        ISNULL(wo.DeviceAmount, 0)
                            AS DeviceAmount,

                        ISNULL(c.Deposit, 0)
                            AS Deposit,

                        ISNULL(p.TotalPaid, 0)
                            AS TotalPaid,



                        -- Dealer threshold
                        (
                            ISNULL(wo.DeviceAmount, 0)
                            + (ISNULL(c.Deposit, 0) * 0.75)
                            + 2000.00
                        ) AS DealerThreshold,



                        -- Total possible dealer commission
                        (
                            ISNULL(c.Total_Cost, 0) * 0.30
                        ) AS DealerCommission,



                        -- Earned commission based on customer payments
                        CASE

                            WHEN
                                ISNULL(p.TotalPaid, 0)
                                >=
                                (
                                    ISNULL(wo.DeviceAmount, 0)
                                    + (ISNULL(c.Deposit, 0) * 0.75)
                                    + 2000.00
                                )

                            THEN
                                (
                                    ISNULL(c.Total_Cost, 0) * 0.30
                                )

                            ELSE
                                (
                                    (
                                        ISNULL(p.TotalPaid, 0)
                                        /
                                        NULLIF
                                        (
                                            (
                                                ISNULL(wo.DeviceAmount, 0)
                                                + (ISNULL(c.Deposit, 0) * 0.75)
                                                + 2000.00
                                            ),
                                            0
                                        )
                                    )
                                    *
                                    (
                                        ISNULL(c.Total_Cost, 0) * 0.30
                                    )
                                )

                        END AS EarnedDealerCommission,



                        -- Already paid to dealer
                        ISNULL(dp.TotalDealerPaid, 0)
                            AS TotalDealerPaid,



                        -- Remaining dealer balance
                        (
                            CASE

                                WHEN
                                    ISNULL(p.TotalPaid, 0)
                                    >=
                                    (
                                        ISNULL(wo.DeviceAmount, 0)
                                        + (ISNULL(c.Deposit, 0) * 0.75)
                                        + 2000.00
                                    )

                                THEN
                                    (
                                        ISNULL(c.Total_Cost, 0) * 0.30
                                    )

                                ELSE
                                    (
                                        (
                                            ISNULL(p.TotalPaid, 0)
                                            /
                                            NULLIF
                                            (
                                                (
                                                    ISNULL(wo.DeviceAmount, 0)
                                                    + (ISNULL(c.Deposit, 0) * 0.75)
                                                    + 2000.00
                                                ),
                                                0
                                            )
                                        )
                                        *
                                        (
                                            ISNULL(c.Total_Cost, 0) * 0.30
                                        )
                                    )

                            END

                            - ISNULL(dp.TotalDealerPaid, 0)

                        ) AS RemainingDealerBalance,



                        -- Status
                        CASE

                            WHEN
                                ISNULL(dp.TotalDealerPaid, 0)
                                >=
                                (
                                    ISNULL(c.Total_Cost, 0) * 0.30
                                )

                            THEN 'FULLY PAID'

                            WHEN
                                ISNULL(dp.TotalDealerPaid, 0) > 0

                            THEN 'PARTIALLY PAID'

                            WHEN
                                ISNULL(p.TotalPaid, 0) > 0

                            THEN 'READY TO PAY'

                            ELSE 'NOT ELIGIBLE'

                        END AS PaymentStatus,



                        d.DeviceGroupId

                    INTO #OutstandingReport

                    FROM dbo.Contract_Info c

                    LEFT JOIN PaymentsSummary p
                        ON p.AccountNoBigint = c.ID

                    LEFT JOIN dbo.Devices d
                        ON d.Id = c.ID

                    INNER JOIN dbo.Woo_Orders wo
                        ON wo.ContractId = c.ContractID

                    LEFT JOIN DealerPaymentsSummary dp
                        ON dp.ContractId = c.ContractID



                    -- only show accounts still owing dealer money
                    WHERE
                        (
                            (
                                CASE

                                    WHEN
                                        ISNULL(p.TotalPaid, 0)
                                        >=
                                        (
                                            ISNULL(wo.DeviceAmount, 0)
                                            + (ISNULL(c.Deposit, 0) * 0.75)
                                            + 2000.00
                                        )

                                    THEN
                                        (
                                            ISNULL(c.Total_Cost, 0) * 0.30
                                        )

                                    ELSE
                                        (
                                            (
                                                ISNULL(p.TotalPaid, 0)
                                                /
                                                NULLIF
                                                (
                                                    (
                                                        ISNULL(wo.DeviceAmount, 0)
                                                        + (ISNULL(c.Deposit, 0) * 0.75)
                                                        + 2000.00
                                                    ),
                                                    0
                                                )
                                            )
                                            *
                                            (
                                                ISNULL(c.Total_Cost, 0) * 0.30
                                            )
                                        )

                                END
                            )

                            - ISNULL(dp.TotalDealerPaid, 0)
                        ) > 0;



                    SELECT *
                    FROM #OutstandingReport
                    WHERE
                        (
                            @DealerId IS NULL
                            OR DeviceGroupId = @DealerId
                        )

                    ORDER BY RemainingDealerBalance DESC

                    OFFSET (@PageNumber - 1) * @PageSize ROWS
                    FETCH NEXT @PageSize ROWS ONLY;



                    SELECT COUNT(*)
                    FROM #OutstandingReport
                    WHERE
                        (
                            @DealerId IS NULL
                            OR DeviceGroupId = @DealerId
                        );



                    DROP TABLE #OutstandingReport;
                    ";

            var parameters = new
            {
                filter.DealerId,
                filter.PageNumber,
                filter.PageSize
            };

            using var multi =
                await _db.QueryMultipleAsync(sql, parameters);

            var items =
                await multi.ReadAsync<OutstandingDealerCommissionReport>();

            var totalCount =
                await multi.ReadFirstAsync<int>();

            return new PagedResult<OutstandingDealerCommissionReport>
            {
                Items = items,
                TotalCount = totalCount,
                PageNumber = filter.PageNumber,
                PageSize = filter.PageSize
            };
        }

        public async Task<PagedResult<DealerCommissionReadyToPayReport>>
DealerCommissionsReadyToPay(
    CommissionsFilter filter)
        {
            var sql = @"

                WITH PaymentsSummary AS
                (
                    SELECT
                        AccountNoBigint,

                        SUM(ISNULL(AmountValue, Amount))
                            AS TotalPaid

                    FROM dbo.KosePayments

                    GROUP BY AccountNoBigint
                ),

                DealerPaymentsSummary AS
                (
                    SELECT
                        ContractId,

                        SUM(ISNULL(AmountPaid, 0))
                            AS TotalDealerPaid

                    FROM dbo.DealerCommissionPayments

                    GROUP BY ContractId
                )

                SELECT
                    c.ContractID,

                    c.ID AS AccountNo,

                    c.First_Name,

                    c.Total_Cost,

                    ISNULL(wo.DeviceAmount, 0)
                        AS DeviceAmount,

                    ISNULL(c.Deposit, 0)
                        AS Deposit,

                    ISNULL(p.TotalPaid, 0)
                        AS TotalPaid,



                    -- Dealer threshold
                    (
                        ISNULL(wo.DeviceAmount, 0)
                        + (ISNULL(c.Deposit, 0) * 0.75)
                        + 2000.00
                    ) AS DealerThreshold,



                    -- Total dealer commission
                    (
                        ISNULL(c.Total_Cost, 0) * 0.30
                    ) AS DealerCommission,



                    -- Earned commission
                    CASE

                        WHEN
                            ISNULL(p.TotalPaid, 0)
                            >=
                            (
                                ISNULL(wo.DeviceAmount, 0)
                                + (ISNULL(c.Deposit, 0) * 0.75)
                                + 2000.00
                            )

                        THEN
                            (
                                ISNULL(c.Total_Cost, 0) * 0.30
                            )

                        ELSE
                            (
                                (
                                    ISNULL(p.TotalPaid, 0)
                                    /
                                    NULLIF
                                    (
                                        (
                                            ISNULL(wo.DeviceAmount, 0)
                                            + (ISNULL(c.Deposit, 0) * 0.75)
                                            + 2000.00
                                        ),
                                        0
                                    )
                                )
                                *
                                (
                                    ISNULL(c.Total_Cost, 0) * 0.30
                                )
                            )

                    END AS EarnedDealerCommission,



                    -- Already paid
                    ISNULL(dp.TotalDealerPaid, 0)
                        AS TotalDealerPaid,



                    -- Amount currently payable
                    (
                        CASE

                            WHEN
                                ISNULL(p.TotalPaid, 0)
                                >=
                                (
                                    ISNULL(wo.DeviceAmount, 0)
                                    + (ISNULL(c.Deposit, 0) * 0.75)
                                    + 2000.00
                                )

                            THEN
                                (
                                    ISNULL(c.Total_Cost, 0) * 0.30
                                )

                            ELSE
                                (
                                    (
                                        ISNULL(p.TotalPaid, 0)
                                        /
                                        NULLIF
                                        (
                                            (
                                                ISNULL(wo.DeviceAmount, 0)
                                                + (ISNULL(c.Deposit, 0) * 0.75)
                                                + 2000.00
                                            ),
                                            0
                                        )
                                    )
                                    *
                                    (
                                        ISNULL(c.Total_Cost, 0) * 0.30
                                    )
                                )

                        END

                        - ISNULL(dp.TotalDealerPaid, 0)

                    ) AS AmountReadyToPay,



                    -- Status
                    CASE

                        WHEN
                            ISNULL(dp.TotalDealerPaid, 0)
                            >=
                            (
                                ISNULL(c.Total_Cost, 0) * 0.30
                            )

                        THEN 'FULLY PAID'

                        WHEN
                            ISNULL(dp.TotalDealerPaid, 0) > 0

                        THEN 'PARTIALLY PAID'

                        WHEN
                            ISNULL(p.TotalPaid, 0) > 0

                        THEN 'READY TO PAY'

                        ELSE 'NOT ELIGIBLE'

                    END AS Status,



                    d.DeviceGroupId

                INTO #ReadyToPayReport

                FROM dbo.Contract_Info c

                LEFT JOIN PaymentsSummary p
                    ON p.AccountNoBigint = c.ID

                LEFT JOIN dbo.Devices d
                    ON d.Id = c.ID

                INNER JOIN dbo.Woo_Orders wo
                    ON wo.ContractId = c.ContractID

                LEFT JOIN DealerPaymentsSummary dp
                    ON dp.ContractId = c.ContractID



                -- only show records with payable balances
                WHERE
                    (
                        (
                            CASE

                                WHEN
                                    ISNULL(p.TotalPaid, 0)
                                    >=
                                    (
                                        ISNULL(wo.DeviceAmount, 0)
                                        + (ISNULL(c.Deposit, 0) * 0.75)
                                        + 2000.00
                                    )

                                THEN
                                    (
                                        ISNULL(c.Total_Cost, 0) * 0.30
                                    )

                                ELSE
                                    (
                                        (
                                            ISNULL(p.TotalPaid, 0)
                                            /
                                            NULLIF
                                            (
                                                (
                                                    ISNULL(wo.DeviceAmount, 0)
                                                    + (ISNULL(c.Deposit, 0) * 0.75)
                                                    + 2000.00
                                                ),
                                                0
                                            )
                                        )
                                        *
                                        (
                                            ISNULL(c.Total_Cost, 0) * 0.30
                                        )
                                    )

                            END
                        )

                        - ISNULL(dp.TotalDealerPaid, 0)
                    ) > 0;



                SELECT *
                FROM #ReadyToPayReport
                WHERE
                    (
                        @DealerId IS NULL
                        OR DeviceGroupId = @DealerId
                    )

                ORDER BY AmountReadyToPay DESC

                OFFSET (@PageNumber - 1) * @PageSize ROWS
                FETCH NEXT @PageSize ROWS ONLY;



                SELECT COUNT(*)
                FROM #ReadyToPayReport
                WHERE
                    (
                        @DealerId IS NULL
                        OR DeviceGroupId = @DealerId
                    );



                DROP TABLE #ReadyToPayReport;
                ";

            var parameters = new
            {
                filter.DealerId,
                filter.PageNumber,
                filter.PageSize
            };

            using var multi =
                await _db.QueryMultipleAsync(sql, parameters);

            var items =
                await multi.ReadAsync<DealerCommissionReadyToPayReport>();

            var totalCount =
                await multi.ReadFirstAsync<int>();

            return new PagedResult<DealerCommissionReadyToPayReport>
            {
                Items = items,
                TotalCount = totalCount,
                PageNumber = filter.PageNumber,
                PageSize = filter.PageSize
            };
        }

        public async Task<PagedResult<AgentsTotalSummaryReport>>
    AgentsTotalSummary(
        CommissionsFilter filter)
        {
            var sql = @"
            WITH AgentSummary AS
            (
                SELECT
                    DebtCollectorUserId AS AgentId,

                    COUNT(*) AS TotalContracts,

                    SUM(ISNULL(Deposit, 0)) AS TotalDeposits,

                    SUM(ISNULL(Deposit, 0) * 0.75)
                        AS TotalAgentCommission

                FROM dbo.Contract_Info

                GROUP BY DebtCollectorUserId
            )

            SELECT *
            FROM AgentSummary
            WHERE
                (
                    @AgentId IS NULL
                    OR AgentId = @AgentId
                )

            ORDER BY TotalAgentCommission DESC

            OFFSET (@PageNumber - 1) * @PageSize ROWS
            FETCH NEXT @PageSize ROWS ONLY;

            SELECT COUNT(*)
            FROM AgentSummary
            WHERE
                (
                    @AgentId IS NULL
                    OR AgentId = @AgentId
                );
            ";

            var parameters = new
            {
                filter.AgentId,
                filter.PageNumber,
                filter.PageSize
            };

            using var multi =
                await _db.QueryMultipleAsync(sql, parameters);

            var items =
                await multi.ReadAsync<AgentsTotalSummaryReport>();

            var totalCount =
                await multi.ReadFirstAsync<int>();

            return new PagedResult<AgentsTotalSummaryReport>
            {
                Items = items,
                TotalCount = totalCount,
                PageNumber = filter.PageNumber,
                PageSize = filter.PageSize
            };
        }

    }
}
