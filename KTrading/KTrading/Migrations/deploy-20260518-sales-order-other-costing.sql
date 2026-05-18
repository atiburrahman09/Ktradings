BEGIN TRANSACTION;
GO

IF COL_LENGTH(N'[dbo].[SalesOrders]', N'OtherCosting') IS NULL
BEGIN
    ALTER TABLE [dbo].[SalesOrders]
        ADD [OtherCosting] decimal(18,4) NOT NULL
            CONSTRAINT [DF_SalesOrders_OtherCosting] DEFAULT 0.0;
END;
GO

IF COL_LENGTH(N'[dbo].[SalesOrders]', N'OtherCostingNote') IS NULL
BEGIN
    ALTER TABLE [dbo].[SalesOrders]
        ADD [OtherCostingNote] nvarchar(1000) NULL;
END;
GO

IF EXISTS (SELECT 1 FROM [dbo].[__EFMigrationsHistory] WHERE [MigrationId] = N'20260518074419_AddSalesOrderOtherCosting')
BEGIN
    PRINT N'Migration 20260518074419_AddSalesOrderOtherCosting already recorded.';
END
ELSE
BEGIN
    INSERT INTO [dbo].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260518074419_AddSalesOrderOtherCosting', N'8.0.13');
END;
GO

COMMIT;
GO

CREATE OR ALTER PROCEDURE [dbo].[usp_GetSalesSummaryAsOnDate]
    @AsOnDate date
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @NextDate date = DATEADD(day, 1, @AsOnDate);

    WITH SalesOrderItemPrices AS
    (
        SELECT
            [SalesOrderId],
            [ProductId],
            CASE
                WHEN SUM([Quantity]) = 0 THEN 0
                ELSE SUM([LineTotal]) / SUM([Quantity])
            END AS [UnitPrice]
        FROM [dbo].[SalesOrderItems]
        GROUP BY [SalesOrderId], [ProductId]
    ),
    ReturnTotals AS
    (
        SELECT
            r.[SalesOrderId],
            SUM(ri.[Quantity] * COALESCE(p.[UnitPrice], 0)) AS [ReturnedAmount]
        FROM [dbo].[ProductReturns] r
        INNER JOIN [dbo].[ProductReturnItems] ri ON ri.[ProductReturnId] = r.[Id]
        LEFT JOIN SalesOrderItemPrices p
            ON p.[SalesOrderId] = r.[SalesOrderId]
            AND p.[ProductId] = ri.[ProductId]
        WHERE r.[SalesOrderId] IS NOT NULL
            AND r.[CreatedAt] < @NextDate
        GROUP BY r.[SalesOrderId]
    )
    SELECT
        @AsOnDate AS AsOnDate,
        COALESCE(SUM(v.[NetSaleAmount]), 0) AS TotalSaleAmount,
        COALESCE(SUM(v.[NetSaleAmount] - so.[Commission] - so.[Khajna] - so.[DsrSalary] - so.[OtherCosting]), 0) AS TotalNetIncome
    FROM [dbo].[SalesOrders] so
    LEFT JOIN ReturnTotals rt ON rt.[SalesOrderId] = so.[Id]
    CROSS APPLY
    (
        SELECT CASE
            WHEN so.[Total] - COALESCE(rt.[ReturnedAmount], 0) < 0 THEN 0
            ELSE so.[Total] - COALESCE(rt.[ReturnedAmount], 0)
        END AS [NetSaleAmount]
    ) v
    WHERE so.[OrderDate] < @NextDate;
END;
GO
