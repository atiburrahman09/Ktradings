SET NOCOUNT ON;

;WITH SalesUnitPrices AS
(
    SELECT
        soi.[SalesOrderId],
        soi.[ProductId],
        CASE
            WHEN SUM(soi.[Quantity]) = 0 THEN CAST(0 AS decimal(18,4))
            ELSE SUM(soi.[LineTotal]) / SUM(soi.[Quantity])
        END AS [UnitPrice]
    FROM [dbo].[SalesOrderItems] soi
    GROUP BY soi.[SalesOrderId], soi.[ProductId]
),
ReturnedAmounts AS
(
    SELECT
        pr.[SalesOrderId],
        SUM(CASE WHEN pri.[Quantity] > 0 THEN pri.[Quantity] ELSE 0 END * COALESCE(sup.[UnitPrice], 0)) AS [ReturnedAmount]
    FROM [dbo].[ProductReturns] pr
    INNER JOIN [dbo].[ProductReturnItems] pri ON pri.[ProductReturnId] = pr.[Id]
    LEFT JOIN SalesUnitPrices sup ON sup.[SalesOrderId] = pr.[SalesOrderId] AND sup.[ProductId] = pri.[ProductId]
    WHERE pr.[SalesOrderId] IS NOT NULL
      AND pri.[IsOutsideSalesDamageReturn] = 0
    GROUP BY pr.[SalesOrderId]
),
OrderDamageAmounts AS
(
    SELECT
        pr.[SalesOrderId],
        SUM(
            CASE
                WHEN pri.[DamagedQuantity] > 0 THEN pri.[DamagedQuantity]
                WHEN pri.[IsDamaged] = 1 THEN pri.[Quantity]
                ELSE 0
            END * COALESCE(sup.[UnitPrice], 0)
        ) AS [DamageAmount]
    FROM [dbo].[ProductReturns] pr
    INNER JOIN [dbo].[ProductReturnItems] pri ON pri.[ProductReturnId] = pr.[Id]
    LEFT JOIN SalesUnitPrices sup ON sup.[SalesOrderId] = pr.[SalesOrderId] AND sup.[ProductId] = pri.[ProductId]
    WHERE pr.[SalesOrderId] IS NOT NULL
      AND pri.[IsOutsideSalesDamageReturn] = 0
    GROUP BY pr.[SalesOrderId]
),
OutsideDamageAmounts AS
(
    SELECT
        so.[Id] AS [SalesOrderId],
        SUM(
            CASE
                WHEN pri.[DamagedQuantity] > 0 THEN pri.[DamagedQuantity]
                WHEN pri.[IsDamaged] = 1 THEN pri.[Quantity]
                ELSE 0
            END * COALESCE(p.[Price], 0)
        ) AS [DamageAmount]
    FROM [dbo].[SalesOrders] so
    INNER JOIN [dbo].[ProductReturns] pr
        ON CAST(pr.[CreatedAt] AS date) = CAST(so.[OrderDate] AS date)
    INNER JOIN [dbo].[ProductReturnItems] pri
        ON pri.[ProductReturnId] = pr.[Id]
    LEFT JOIN [dbo].[Products] p
        ON p.[Id] = pri.[ProductId]
    WHERE pri.[IsOutsideSalesDamageReturn] = 1
      AND NOT EXISTS
      (
          SELECT 1
          FROM [dbo].[SalesOrderItems] soi
          WHERE soi.[SalesOrderId] = so.[Id]
            AND soi.[ProductId] = pri.[ProductId]
      )
    GROUP BY so.[Id]
),
CorrectedSalesOrders AS
(
    SELECT
        so.[Id],
        so.[OrderNumber],
        so.[OrderDate],
        CASE
            WHEN (CASE WHEN so.[Total] - COALESCE(ra.[ReturnedAmount], 0) > 0 THEN so.[Total] - COALESCE(ra.[ReturnedAmount], 0) ELSE 0 END)
                 - so.[Commission]
                 - so.[DsrSalary]
                 - COALESCE(oda.[DamageAmount], 0)
                 - COALESCE(xda.[DamageAmount], 0)
                 - so.[OtherCosting]
                 - so.[DueAmount] > 0
            THEN (CASE WHEN so.[Total] - COALESCE(ra.[ReturnedAmount], 0) > 0 THEN so.[Total] - COALESCE(ra.[ReturnedAmount], 0) ELSE 0 END)
                 - so.[Commission]
                 - so.[DsrSalary]
                 - COALESCE(oda.[DamageAmount], 0)
                 - COALESCE(xda.[DamageAmount], 0)
                 - so.[OtherCosting]
                 - so.[DueAmount]
            ELSE 0
        END AS [CorrectPaidAmount]
    FROM [dbo].[SalesOrders] so
    LEFT JOIN ReturnedAmounts ra ON ra.[SalesOrderId] = so.[Id]
    LEFT JOIN OrderDamageAmounts oda ON oda.[SalesOrderId] = so.[Id]
    LEFT JOIN OutsideDamageAmounts xda ON xda.[SalesOrderId] = so.[Id]
),
InitialPayments AS
(
    SELECT
        p.[Id],
        p.[SalesOrderId],
        ROW_NUMBER() OVER (PARTITION BY p.[SalesOrderId] ORDER BY p.[PaymentDate], p.[Id]) AS [PaymentRank]
    FROM [dbo].[Payments] p
    WHERE p.[Reference] LIKE N'Initial payment for %'
),
OtherPayments AS
(
    SELECT
        p.[SalesOrderId],
        SUM(p.[Amount]) AS [Amount]
    FROM [dbo].[Payments] p
    LEFT JOIN InitialPayments ip ON ip.[Id] = p.[Id] AND ip.[PaymentRank] = 1
    WHERE ip.[Id] IS NULL
    GROUP BY p.[SalesOrderId]
),
CorrectedInitialPayments AS
(
    SELECT
        cso.[Id] AS [SalesOrderId],
        cso.[OrderNumber],
        cso.[OrderDate],
        ip.[Id] AS [InitialPaymentId],
        CASE
            WHEN cso.[CorrectPaidAmount] - COALESCE(op.[Amount], 0) > 0
            THEN cso.[CorrectPaidAmount] - COALESCE(op.[Amount], 0)
            ELSE 0
        END AS [CorrectInitialPaymentAmount]
    FROM CorrectedSalesOrders cso
    LEFT JOIN InitialPayments ip ON ip.[SalesOrderId] = cso.[Id] AND ip.[PaymentRank] = 1
    LEFT JOIN OtherPayments op ON op.[SalesOrderId] = cso.[Id]
)
UPDATE so
SET so.[PaidAmount] = cso.[CorrectPaidAmount]
FROM [dbo].[SalesOrders] so
INNER JOIN CorrectedSalesOrders cso ON cso.[Id] = so.[Id];

;WITH InitialPayments AS
(
    SELECT
        p.[Id],
        p.[SalesOrderId],
        ROW_NUMBER() OVER (PARTITION BY p.[SalesOrderId] ORDER BY p.[PaymentDate], p.[Id]) AS [PaymentRank]
    FROM [dbo].[Payments] p
    WHERE p.[Reference] LIKE N'Initial payment for %'
),
OtherPayments AS
(
    SELECT
        p.[SalesOrderId],
        SUM(p.[Amount]) AS [Amount]
    FROM [dbo].[Payments] p
    LEFT JOIN InitialPayments ip ON ip.[Id] = p.[Id] AND ip.[PaymentRank] = 1
    WHERE ip.[Id] IS NULL
    GROUP BY p.[SalesOrderId]
)
UPDATE p
SET
    p.[Amount] = CASE
        WHEN so.[PaidAmount] - COALESCE(op.[Amount], 0) > 0 THEN so.[PaidAmount] - COALESCE(op.[Amount], 0)
        ELSE 0
    END,
    p.[PaymentDate] = so.[OrderDate],
    p.[Reference] = CONCAT(N'Initial payment for ', so.[OrderNumber])
FROM [dbo].[Payments] p
INNER JOIN InitialPayments ip ON ip.[Id] = p.[Id] AND ip.[PaymentRank] = 1
INNER JOIN [dbo].[SalesOrders] so ON so.[Id] = p.[SalesOrderId]
LEFT JOIN OtherPayments op ON op.[SalesOrderId] = so.[Id];

DELETE p
FROM [dbo].[Payments] p
INNER JOIN [dbo].[SalesOrders] so ON so.[Id] = p.[SalesOrderId]
WHERE p.[Reference] LIKE N'Initial payment for %'
  AND p.[Amount] <= 0;
