/*
    Roll back one sales order and its dependent records.

    Usage:
      1. Set either @SalesOrderId or @OrderNumber.
      2. Run with @PreviewOnly = 1 first to review counts.
      3. Set @PreviewOnly = 0 to perform the rollback.

    Notes:
      - OUT movements for the sales order are reversed back into normal stock.
      - RETURN movements for product returns linked to the sales order are reversed out of normal stock.
      - DAMAGE movements are deleted but do not change normal stock, matching application behavior.
      - Product returns linked to the sales order are removed with their items.
      - Payments linked to the sales order are removed.
*/

SET XACT_ABORT ON;

DECLARE @SalesOrderId uniqueidentifier = NULL;
DECLARE @OrderNumber nvarchar(100) = N''; -- Example: N'SO-0001'
DECLARE @PreviewOnly bit = 1;

IF @SalesOrderId IS NULL
BEGIN
    SELECT @SalesOrderId = Id
    FROM dbo.SalesOrders
    WHERE OrderNumber = @OrderNumber;
END;

IF @SalesOrderId IS NULL
BEGIN
    THROW 50001, 'Sales order was not found. Set @SalesOrderId or @OrderNumber.', 1;
END;

DECLARE @ReturnIds TABLE (Id uniqueidentifier PRIMARY KEY);
INSERT INTO @ReturnIds (Id)
SELECT Id
FROM dbo.ProductReturns
WHERE SalesOrderId = @SalesOrderId;

DECLARE @StockEffects TABLE
(
    ProductId uniqueidentifier PRIMARY KEY,
    QuantityDelta decimal(18,4) NOT NULL
);

INSERT INTO @StockEffects (ProductId, QuantityDelta)
SELECT
    ProductId,
    SUM(-Quantity) AS QuantityDelta
FROM dbo.StockMovements
WHERE ReferenceId = @SalesOrderId
    AND MovementType <> N'DAMAGE'
GROUP BY ProductId;

MERGE @StockEffects AS target
USING
(
    SELECT
        ProductId,
        SUM(-Quantity) AS QuantityDelta
    FROM dbo.StockMovements
    WHERE ReferenceId IN (SELECT Id FROM @ReturnIds)
        AND MovementType <> N'DAMAGE'
    GROUP BY ProductId
) AS source
ON target.ProductId = source.ProductId
WHEN MATCHED THEN
    UPDATE SET QuantityDelta = target.QuantityDelta + source.QuantityDelta
WHEN NOT MATCHED THEN
    INSERT (ProductId, QuantityDelta)
    VALUES (source.ProductId, source.QuantityDelta);

SELECT
    @SalesOrderId AS SalesOrderId,
    (SELECT OrderNumber FROM dbo.SalesOrders WHERE Id = @SalesOrderId) AS OrderNumber,
    (SELECT COUNT(*) FROM dbo.SalesOrderItems WHERE SalesOrderId = @SalesOrderId) AS SalesOrderItemCount,
    (SELECT COUNT(*) FROM dbo.Payments WHERE SalesOrderId = @SalesOrderId) AS PaymentCount,
    (SELECT COUNT(*) FROM @ReturnIds) AS ProductReturnCount,
    (SELECT COUNT(*) FROM dbo.ProductReturnItems WHERE ProductReturnId IN (SELECT Id FROM @ReturnIds)) AS ProductReturnItemCount,
    (SELECT COUNT(*) FROM dbo.StockMovements WHERE ReferenceId = @SalesOrderId OR ReferenceId IN (SELECT Id FROM @ReturnIds)) AS StockMovementCount;

SELECT
    p.Name AS ProductName,
    p.SKU,
    s.ProductId,
    s.Quantity AS CurrentStock,
    e.QuantityDelta,
    s.Quantity + e.QuantityDelta AS StockAfterRollback
FROM @StockEffects e
LEFT JOIN dbo.Stocks s ON s.ProductId = e.ProductId
LEFT JOIN dbo.Products p ON p.Id = e.ProductId
ORDER BY p.Name;

IF @PreviewOnly = 1
BEGIN
    PRINT N'Preview only. Set @PreviewOnly = 0 to perform rollback.';
    RETURN;
END;

BEGIN TRANSACTION;

UPDATE s
SET
    s.Quantity = s.Quantity + e.QuantityDelta,
    s.UpdatedAt = SYSDATETIMEOFFSET()
FROM dbo.Stocks s
INNER JOIN @StockEffects e ON e.ProductId = s.ProductId;

INSERT INTO dbo.Stocks (Id, ProductId, Quantity, UpdatedAt)
SELECT
    NEWID(),
    e.ProductId,
    e.QuantityDelta,
    SYSDATETIMEOFFSET()
FROM @StockEffects e
WHERE NOT EXISTS
(
    SELECT 1
    FROM dbo.Stocks s
    WHERE s.ProductId = e.ProductId
);

DELETE FROM dbo.StockMovements
WHERE ReferenceId = @SalesOrderId
    OR ReferenceId IN (SELECT Id FROM @ReturnIds);

DELETE FROM dbo.ProductReturnItems
WHERE ProductReturnId IN (SELECT Id FROM @ReturnIds);

DELETE FROM dbo.ProductReturns
WHERE Id IN (SELECT Id FROM @ReturnIds);

DELETE FROM dbo.Payments
WHERE SalesOrderId = @SalesOrderId;

DELETE FROM dbo.SalesOrderItems
WHERE SalesOrderId = @SalesOrderId;

DELETE FROM dbo.SalesOrders
WHERE Id = @SalesOrderId;

COMMIT TRANSACTION;

PRINT N'Sales order rollback completed.';
