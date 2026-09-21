-- Checks every table/column the dashboard rollup + watchlist + commission
-- work depends on, and reports what's MISSING. Read-only -- doesn't create
-- or change anything. Run this on staging and look for any row where
-- Status <> 'OK'.

;WITH Checks AS (
    -- New tables from our own scripts
    SELECT 'Table' AS Kind, 'DashboardSnapshot' AS Name, '001_create_dashboard_tables.sql' AS Script,
        CASE WHEN EXISTS (SELECT 1 FROM sys.tables WHERE name = 'DashboardSnapshot') THEN 'OK' ELSE 'MISSING' END AS Status
    UNION ALL
    SELECT 'Table', 'DashboardMonthlyTrend', '001_create_dashboard_tables.sql',
        CASE WHEN EXISTS (SELECT 1 FROM sys.tables WHERE name = 'DashboardMonthlyTrend') THEN 'OK' ELSE 'MISSING' END
    UNION ALL
    SELECT 'Table', 'DashboardWatchlistEntry', '001_create_dashboard_tables.sql',
        CASE WHEN EXISTS (SELECT 1 FROM sys.tables WHERE name = 'DashboardWatchlistEntry') THEN 'OK' ELSE 'MISSING' END
    UNION ALL
    SELECT 'Table', 'DashboardPerformanceEntry', '001_create_dashboard_tables.sql',
        CASE WHEN EXISTS (SELECT 1 FROM sys.tables WHERE name = 'DashboardPerformanceEntry') THEN 'OK' ELSE 'MISSING' END
    UNION ALL
    SELECT 'Table', 'DashboardDeviceStock', '001_create_dashboard_tables.sql',
        CASE WHEN EXISTS (SELECT 1 FROM sys.tables WHERE name = 'DashboardDeviceStock') THEN 'OK' ELSE 'MISSING' END
    UNION ALL
    SELECT 'Table', 'DashboardCompletedContract', '001_create_dashboard_tables.sql',
        CASE WHEN EXISTS (SELECT 1 FROM sys.tables WHERE name = 'DashboardCompletedContract') THEN 'OK' ELSE 'MISSING' END
    UNION ALL
    SELECT 'Column', 'DashboardCompletedContract.Status', '002_extend_completed_contracts.sql',
        CASE WHEN EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('DashboardCompletedContract') AND name = 'Status') THEN 'OK' ELSE 'MISSING' END
    UNION ALL
    SELECT 'Column', 'DashboardCompletedContract.PctComplete', '002_extend_completed_contracts.sql',
        CASE WHEN EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('DashboardCompletedContract') AND name = 'PctComplete') THEN 'OK' ELSE 'MISSING' END
    UNION ALL
    SELECT 'Column (nullable)', 'DashboardCompletedContract.CompletedDate', '002_extend_completed_contracts.sql',
        CASE WHEN EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('DashboardCompletedContract') AND name = 'CompletedDate' AND is_nullable = 1) THEN 'OK' ELSE 'MISSING/NOT NULLABLE' END
    UNION ALL
    SELECT 'Column', 'DashboardSnapshot.CommissionReceived', '003_add_commission_snapshot_fields.sql',
        CASE WHEN EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('DashboardSnapshot') AND name = 'CommissionReceived') THEN 'OK' ELSE 'MISSING' END
    UNION ALL
    SELECT 'Column', 'DashboardSnapshot.CommissionPaidToAgents', '003_add_commission_snapshot_fields.sql',
        CASE WHEN EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('DashboardSnapshot') AND name = 'CommissionPaidToAgents') THEN 'OK' ELSE 'MISSING' END
    UNION ALL
    SELECT 'Column', 'DashboardSnapshot.CommissionOutstanding', '003_add_commission_snapshot_fields.sql',
        CASE WHEN EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('DashboardSnapshot') AND name = 'CommissionOutstanding') THEN 'OK' ELSE 'MISSING' END
    UNION ALL
    SELECT 'Table', 'AgentCommissionPayments', 'Commissions/001_create_agent_commission_payments.sql',
        CASE WHEN EXISTS (SELECT 1 FROM sys.tables WHERE name = 'AgentCommissionPayments') THEN 'OK' ELSE 'MISSING' END
    UNION ALL
    SELECT 'Column', 'AgentCommissionPayments.AmountPaid', 'Commissions/001_create_agent_commission_payments.sql',
        CASE WHEN EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('AgentCommissionPayments') AND name = 'AmountPaid') THEN 'OK' ELSE 'MISSING' END
    UNION ALL
    SELECT 'Column', 'AgentCommissionPayments.ContractId', 'Commissions/001_create_agent_commission_payments.sql',
        CASE WHEN EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('AgentCommissionPayments') AND name = 'ContractId') THEN 'OK' ELSE 'MISSING' END
    UNION ALL
    SELECT 'Source column', 'DealerCommissionPayments.AmountPaid', '(pre-existing)',
        CASE WHEN EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('DealerCommissionPayments') AND name = 'AmountPaid') THEN 'OK' ELSE 'MISSING' END
    UNION ALL
    SELECT 'Source column', 'DealerCommissionPayments.ContractId', '(pre-existing)',
        CASE WHEN EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('DealerCommissionPayments') AND name = 'ContractId') THEN 'OK' ELSE 'MISSING' END
    UNION ALL
    SELECT 'Source column', 'Contract_Info.Term_in_Months', '(pre-existing)',
        CASE WHEN EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Contract_Info') AND name = 'Term_in_Months') THEN 'OK' ELSE 'MISSING' END
    UNION ALL
    SELECT 'Source column', 'Contract_Info.ContractID', '(pre-existing)',
        CASE WHEN EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Contract_Info') AND name = 'ContractID') THEN 'OK' ELSE 'MISSING' END
    UNION ALL
    SELECT 'Source column', 'Users.Name', '(pre-existing)',
        CASE WHEN EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Users') AND name = 'Name') THEN 'OK' ELSE 'MISSING' END
    UNION ALL
    SELECT 'Source column', 'Users.LastName', '(pre-existing)',
        CASE WHEN EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Users') AND name = 'LastName') THEN 'OK' ELSE 'MISSING' END
    UNION ALL
    SELECT 'Table (separate feature, not needed for population script)', 'AccountWatchlist', 'Watchlist/001_create_account_watchlist.sql',
        CASE WHEN EXISTS (SELECT 1 FROM sys.tables WHERE name = 'AccountWatchlist') THEN 'OK' ELSE 'MISSING' END

    UNION ALL

    -- Pre-existing source tables/columns everything above depends on
    SELECT 'Source table', 'Contract_Info', '(pre-existing)',
        CASE WHEN EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Contract_Info') THEN 'OK' ELSE 'MISSING' END
    UNION ALL
    SELECT 'Source column', 'Contract_Info.AssignedAgentId', '(pre-existing)',
        CASE WHEN EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Contract_Info') AND name = 'AssignedAgentId') THEN 'OK' ELSE 'MISSING' END
    UNION ALL
    SELECT 'Source column', 'Contract_Info.BuyingPrice', '(pre-existing)',
        CASE WHEN EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Contract_Info') AND name = 'BuyingPrice') THEN 'OK' ELSE 'MISSING' END
    UNION ALL
    SELECT 'Source column', 'Contract_Info.StartDate', '(pre-existing)',
        CASE WHEN EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Contract_Info') AND name = 'StartDate') THEN 'OK' ELSE 'MISSING' END
    UNION ALL
    SELECT 'Source table', 'Devices', '(pre-existing)',
        CASE WHEN EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Devices') THEN 'OK' ELSE 'MISSING' END
    UNION ALL
    SELECT 'Source table', 'Dealers', '(pre-existing)',
        CASE WHEN EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Dealers') THEN 'OK' ELSE 'MISSING' END
    UNION ALL
    SELECT 'Source table', 'KosePayments', '(pre-existing)',
        CASE WHEN EXISTS (SELECT 1 FROM sys.tables WHERE name = 'KosePayments') THEN 'OK' ELSE 'MISSING' END
    UNION ALL
    SELECT 'Source table', 'OrphanedPayments', '(pre-existing)',
        CASE WHEN EXISTS (SELECT 1 FROM sys.tables WHERE name = 'OrphanedPayments') THEN 'OK' ELSE 'MISSING' END
    UNION ALL
    SELECT 'Source table', 'DealerCommissionPayments', '(pre-existing)',
        CASE WHEN EXISTS (SELECT 1 FROM sys.tables WHERE name = 'DealerCommissionPayments') THEN 'OK' ELSE 'MISSING' END
    UNION ALL
    SELECT 'Source table', 'Users', '(pre-existing)',
        CASE WHEN EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Users') THEN 'OK' ELSE 'MISSING' END
)
SELECT * FROM Checks ORDER BY CASE WHEN Status = 'OK' THEN 1 ELSE 0 END, Kind, Name;
