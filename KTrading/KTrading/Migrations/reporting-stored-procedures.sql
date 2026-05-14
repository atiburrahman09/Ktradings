CREATE OR ALTER PROCEDURE [dbo].[usp_GetSalesSummaryAsOnDate]
    @AsOnDate date
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @NextDate date = DATEADD(day, 1, @AsOnDate);

    SELECT
        @AsOnDate AS AsOnDate,
        COALESCE(SUM([Total]), 0) AS TotalSaleAmount,
        COALESCE(SUM([Total] - [Commission] - [Khajna] - [DsrSalary]), 0) AS TotalNetIncome
    FROM [dbo].[SalesOrders]
    WHERE [OrderDate] < @NextDate;
END;
GO

