-- Dashboard rollup tables.
--
-- These are precomputed by the nightly ScheduledDashboardRollup job and read
-- by DashboardReportRepository. They are NOT written to directly by request
-- handlers -- the web app only ever reads from these tables on the request
-- path, keeping dashboard page loads independent of ledger size.
--
-- Scope columns (DealerId, AgentId) are both nullable:
--   DealerId = NULL, AgentId = NULL  -> company-wide (Admin dashboard) row
--   DealerId = <id>, AgentId = NULL  -> one dealer (Dealer dashboard) row
--   DealerId = <id>, AgentId = <id>  -> one agent within a dealer (future Agent dashboard)
--
-- Safe to re-run: every CREATE is guarded with an existence check.
--
-- Every metric column in DashboardSnapshot is nullable. The refresh job
-- populates whichever subset it currently knows how to compute correctly and
-- leaves the rest NULL; DashboardReportService applies each field
-- independently with a "?? keep sample data" fallback, so a partially
-- populated row never overwrites not-yet-computed metrics with zeros.

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'DashboardSnapshot')
BEGIN
    CREATE TABLE DashboardSnapshot
    (
        DealerId                        INT NULL,
        AgentId                         INT NULL,

        RevenueThisMonth                DECIMAL(18,2) NULL,
        RevenueGrowthPct                DECIMAL(9,2)  NULL,
        AvgPerAccount                   DECIMAL(18,2) NULL,

        TotalAccounts                   INT NULL,
        ActivePct                       DECIMAL(9,2)  NULL,
        NewThisMonth                    INT NULL,
        InDefault                       INT NULL,
        DefaultRatePct                  DECIMAL(9,2)  NULL,
        NonPayingChange                 INT NULL,

        ArrearsTotal                    DECIMAL(18,2) NULL,
        ArrearsChangePct                DECIMAL(9,2)  NULL,

        BadDebtThisMonth                DECIMAL(18,2) NULL,
        BadDebtChangePct                DECIMAL(9,2)  NULL,

        ActiveRateVsTargetPct           DECIMAL(9,2)  NULL,

        PortfolioGoodPct                DECIMAL(9,2)  NULL,
        PortfolioSlowPct                DECIMAL(9,2)  NULL,
        PortfolioArrearsPct             DECIMAL(9,2)  NULL,
        PortfolioNonPayingPct           DECIMAL(9,2)  NULL,
        PortfolioGoodPctChange          DECIMAL(9,2)  NULL,

        CollectionRatePct               DECIMAL(9,2)  NULL,
        CollectionRateChangePct         DECIMAL(9,2)  NULL,
        PortfolioAtRiskPct              DECIMAL(9,2)  NULL,
        PortfolioAtRiskChangePct        DECIMAL(9,2)  NULL,

        RepeatCustomerRatePct           DECIMAL(9,2)  NULL,
        AvgCustomerLifetimeValue        DECIMAL(18,2) NULL,
        ChurnRatePct                    DECIMAL(9,2)  NULL,

        CompletedContractsThisMonth     INT NULL,
        CompletedContractsChangePct     DECIMAL(9,2)  NULL,
        ContractCompletionRatePct       DECIMAL(9,2)  NULL,
        ContractCompletionRateChangePct DECIMAL(9,2)  NULL,
        AvgTimeToCompletionMonths       DECIMAL(9,2)  NULL,
        TotalValueCompletedThisMonth    DECIMAL(18,2) NULL,

        -- Admin-only fields (NULL for dealer/agent-scoped rows)
        RevenueTargetThisMonth          DECIMAL(18,2) NULL,
        GoodAccounts                    INT NULL,
        BadAccounts                     INT NULL,
        PayingAccounts                  INT NULL,
        NonPayingAccounts               INT NULL,
        CostOfDevicesThisMonth          DECIMAL(18,2) NULL,
        NetProfitChangePct              DECIMAL(9,2)  NULL,
        ProfitMarginChangePct           DECIMAL(9,2)  NULL,
        ProfitMarginTargetPct           DECIMAL(9,2)  NULL,
        OperatingExpensesThisMonth      DECIMAL(18,2) NULL,
        TaxRatePct                      DECIMAL(9,2)  NULL,
        DividendsPaidThisMonth          DECIMAL(18,2) NULL,
        TotalCustomers                  INT NULL,
        NewCustomersThisMonth           INT NULL,

        RefreshedAtUtc                  DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),

        CONSTRAINT UQ_DashboardSnapshot_Scope UNIQUE (DealerId, AgentId)
    );

    CREATE INDEX IX_DashboardSnapshot_DealerId ON DashboardSnapshot (DealerId);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'DashboardMonthlyTrend')
BEGIN
    CREATE TABLE DashboardMonthlyTrend
    (
        DealerId      INT NULL,
        AgentId       INT NULL,
        YearMonth     CHAR(7) NOT NULL,           -- 'YYYY-MM'
        Revenue       DECIMAL(18,2) NOT NULL DEFAULT 0,
        AccountsCount INT NOT NULL DEFAULT 0,

        CONSTRAINT UQ_DashboardMonthlyTrend_Scope UNIQUE (DealerId, AgentId, YearMonth)
    );

    CREATE INDEX IX_DashboardMonthlyTrend_DealerId ON DashboardMonthlyTrend (DealerId, YearMonth);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'DashboardWatchlistEntry')
BEGIN
    CREATE TABLE DashboardWatchlistEntry
    (
        Id             INT IDENTITY(1,1) PRIMARY KEY,
        ScopeDealerId  INT NULL,
        ScopeAgentId   INT NULL,
        WatchlistType  VARCHAR(20) NOT NULL,       -- 'NonPayer' | 'SlowPayer' | 'GoodPayer'
        Rank           INT NOT NULL,
        AccountId      BIGINT NULL,
        CustomerName   NVARCHAR(200) NOT NULL,
        AgentName      NVARCHAR(200) NULL,
        DealerName     NVARCHAR(200) NULL,          -- populated for admin-scope rows only
        Phone          NVARCHAR(30) NULL,
        Detail         NVARCHAR(100) NOT NULL
    );

    CREATE INDEX IX_DashboardWatchlistEntry_Scope
        ON DashboardWatchlistEntry (ScopeDealerId, ScopeAgentId, WatchlistType, Rank);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'DashboardPerformanceEntry')
BEGIN
    CREATE TABLE DashboardPerformanceEntry
    (
        Id             INT IDENTITY(1,1) PRIMARY KEY,
        ScopeDealerId  INT NULL,                   -- NULL => dealer-performance row (admin scope)
        ScopeAgentId   INT NULL,                   -- NULL => this row IS a dealer/agent summary row, not a further breakdown
        EntryType      VARCHAR(20) NOT NULL,        -- 'Dealer' | 'Agent'
        Rank           INT NOT NULL,
        SubjectId      INT NOT NULL,                -- DealerId or AgentId (UserId) being ranked
        SubjectName    NVARCHAR(200) NOT NULL,
        ParentName     NVARCHAR(200) NULL,           -- DealerName, when EntryType = 'Agent'
        Accounts       INT NOT NULL DEFAULT 0,
        ActivePct      DECIMAL(9,2) NOT NULL DEFAULT 0,
        Revenue        DECIMAL(18,2) NULL,
        CommissionPaid DECIMAL(18,2) NULL,
        CommissionDue  DECIMAL(18,2) NULL,
        PctOfTarget    DECIMAL(9,2) NOT NULL DEFAULT 0
    );

    CREATE INDEX IX_DashboardPerformanceEntry_Scope
        ON DashboardPerformanceEntry (ScopeDealerId, EntryType, Rank);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'DashboardDeviceStock')
BEGIN
    CREATE TABLE DashboardDeviceStock
    (
        Id             INT IDENTITY(1,1) PRIMARY KEY,
        ScopeDealerId  INT NULL,
        ScopeAgentId   INT NULL,
        DeviceGroupId  INT NULL,
        DeviceName     NVARCHAR(200) NOT NULL,
        Units          INT NOT NULL DEFAULT 0,
        AvgValue       DECIMAL(18,2) NOT NULL DEFAULT 0,
        GoodPct        DECIMAL(9,2) NOT NULL DEFAULT 0,
        ArrearsPct     DECIMAL(9,2) NOT NULL DEFAULT 0
    );

    CREATE INDEX IX_DashboardDeviceStock_Scope ON DashboardDeviceStock (ScopeDealerId, ScopeAgentId);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'DashboardCompletedContract')
BEGIN
    CREATE TABLE DashboardCompletedContract
    (
        Id             INT IDENTITY(1,1) PRIMARY KEY,
        ScopeDealerId  INT NULL,
        ScopeAgentId   INT NULL,
        AccountId      BIGINT NULL,
        CustomerName   NVARCHAR(200) NOT NULL,
        DealerName     NVARCHAR(200) NULL,          -- populated for admin-scope rows only
        ProductName    NVARCHAR(200) NOT NULL,
        CompletedDate  DATE NOT NULL,
        TotalPaid      DECIMAL(18,2) NOT NULL DEFAULT 0,
        DurationMonths INT NOT NULL DEFAULT 0
    );

    CREATE INDEX IX_DashboardCompletedContract_Scope
        ON DashboardCompletedContract (ScopeDealerId, ScopeAgentId, CompletedDate DESC);
END
GO
