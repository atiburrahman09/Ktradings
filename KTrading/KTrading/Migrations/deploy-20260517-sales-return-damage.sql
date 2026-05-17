BEGIN TRANSACTION;
GO

IF COL_LENGTH(N'[dbo].[ProductReturnItems]', N'DamagedQuantity') IS NULL
BEGIN
    ALTER TABLE [dbo].[ProductReturnItems]
        ADD [DamagedQuantity] decimal(18,2) NOT NULL
            CONSTRAINT [DF_ProductReturnItems_DamagedQuantity] DEFAULT 0.0;
END;
GO

UPDATE [dbo].[ProductReturnItems]
SET [DamagedQuantity] = [Quantity]
WHERE [IsDamaged] = 1
    AND [DamagedQuantity] = 0;
GO

IF EXISTS (SELECT 1 FROM [dbo].[__EFMigrationsHistory] WHERE [MigrationId] = N'20260517090000_AddProductReturnDamagedQuantity')
BEGIN
    PRINT N'Migration 20260517090000_AddProductReturnDamagedQuantity already recorded.';
END
ELSE
BEGIN
    INSERT INTO [dbo].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260517090000_AddProductReturnDamagedQuantity', N'8.0.13');
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
        COALESCE(SUM(v.[NetSaleAmount] - so.[Commission] - so.[Khajna] - so.[DsrSalary]), 0) AS TotalNetIncome
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
