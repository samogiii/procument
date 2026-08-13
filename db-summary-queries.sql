/* ============================================================================
   Procument — Read-only summary & join query library
   ----------------------------------------------------------------------------
   SQL Server / schema [dbo]. Every statement here is SELECT-only.

   Companion file: db-schema-reference.md (full table + column + FK reference).

   HOUSE RULES baked into every query below — keep them when you write new ones:
     1. Half-open date ranges:  >= @From AND < @ToExcl.  Never BETWEEN on datetime2.
     2. Multi-metric summaries use ISOLATED aggregates (one CTE or scalar subquery
        per metric). Never SUM across a chain of LEFT JOINs — a parent SUM silently
        multiplies by the child row count and the output still looks plausible.
     3. Any 1:N child is pre-aggregated to the anchor grain BEFORE joining,
        or collapsed with OUTER APPLY (SELECT TOP 1 ... ORDER BY <deterministic>).
     4. Nullable FK  ->  LEFT JOIN.  PurchaseOrders.InvoiceId and POItems.POId are
        nullable; INNER JOIN there silently deletes real rows.
     5. Cancelled sales orders are excluded from money figures (i.IsCancelled = 0).
     6. Anti-joins use NOT EXISTS, never NOT IN (a single NULL makes NOT IN empty).
     7. Integer division truncates — cast to decimal before dividing.
     8. Grain is stated above every query. One row per <X>.
   ============================================================================ */


/* ============================================================================
   A. SCHEMA INTROSPECTION — run these first to verify the schema is current
   ============================================================================ */

-- A1. Every table with its row count. Grain: one row per table.
SELECT  s.name AS SchemaName,
        t.name AS TableName,
        SUM(p.rows) AS ApproxRowCount
FROM sys.tables t
JOIN sys.schemas s    ON s.schema_id = t.schema_id
JOIN sys.partitions p ON p.object_id = t.object_id AND p.index_id IN (0, 1)
GROUP BY s.name, t.name
ORDER BY SUM(p.rows) DESC;

-- A2. Every column of one table (authoritative — prose docs can drift, this cannot).
SELECT  COLUMN_NAME, DATA_TYPE, CHARACTER_MAXIMUM_LENGTH,
        NUMERIC_PRECISION, NUMERIC_SCALE, IS_NULLABLE, COLUMN_DEFAULT
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'ProcurementItems'          -- <- change table name
ORDER BY ORDINAL_POSITION;

-- A3. Every foreign key in the database — the complete join map.
-- Grain: one row per FK column.
SELECT  fk.name                AS ForeignKeyName,
        pc.name                AS ChildTable,
        cc.name                AS ChildColumn,
        pr.name                AS ParentTable,
        rc.name                AS ParentColumn,
        cc.is_nullable         AS ChildColumnIsNullable   -- 1 => use LEFT JOIN
FROM sys.foreign_keys fk
JOIN sys.foreign_key_columns fkc ON fkc.constraint_object_id = fk.object_id
JOIN sys.tables  pc ON pc.object_id = fk.parent_object_id
JOIN sys.columns cc ON cc.object_id = fkc.parent_object_id     AND cc.column_id = fkc.parent_column_id
JOIN sys.tables  pr ON pr.object_id = fk.referenced_object_id
JOIN sys.columns rc ON rc.object_id = fkc.referenced_object_id AND rc.column_id = fkc.referenced_column_id
ORDER BY pc.name, cc.name;

-- A4. Distinct status values actually present, with counts.
-- Use this instead of trusting a documented status list.
SELECT 'RFQs'           AS TableName, CAST(Status AS nvarchar(100)) AS StatusValue, COUNT(*) AS RowCnt FROM [dbo].[RFQs] GROUP BY Status
UNION ALL SELECT 'Quotes',           CAST(Status AS nvarchar(100)), COUNT(*) FROM [dbo].[Quotes]           GROUP BY Status
UNION ALL SELECT 'Invoices',         CAST(Status AS nvarchar(100)), COUNT(*) FROM [dbo].[Invoices]         GROUP BY Status
UNION ALL SELECT 'Procurements',     CAST(Status AS nvarchar(100)), COUNT(*) FROM [dbo].[Procurements]     GROUP BY Status
UNION ALL SELECT 'PurchaseOrders',   CAST(Status AS nvarchar(100)), COUNT(*) FROM [dbo].[PurchaseOrders]   GROUP BY Status
UNION ALL SELECT 'FinalInvoices',    CAST(Status AS nvarchar(100)), COUNT(*) FROM [dbo].[FinalInvoices]    GROUP BY Status
UNION ALL SELECT 'ShipmentNotes',    CAST(Status AS nvarchar(100)), COUNT(*) FROM [dbo].[ShipmentNotes]    GROUP BY Status
UNION ALL SELECT 'Suppliers',        CAST(Status AS nvarchar(100)), COUNT(*) FROM [dbo].[Suppliers]        GROUP BY Status
ORDER BY TableName, RowCnt DESC;

-- A5. Data coverage — how far back each fact table goes, and how fresh it is.
-- Grain: one row per table. Tells you what date range a report can honestly cover.
SELECT 'RFQs' AS TableName, COUNT(*) AS RowCnt, MIN(CreatedAt) AS FirstRow, MAX(CreatedAt) AS LastRow FROM [dbo].[RFQs]
UNION ALL SELECT 'Quotes',         COUNT(*), MIN(CreatedAt), MAX(CreatedAt) FROM [dbo].[Quotes]
UNION ALL SELECT 'Invoices',       COUNT(*), MIN(CreatedAt), MAX(CreatedAt) FROM [dbo].[Invoices]
UNION ALL SELECT 'Procurements',   COUNT(*), MIN(CreatedAt), MAX(CreatedAt) FROM [dbo].[Procurements]
UNION ALL SELECT 'PurchaseOrders', COUNT(*), MIN(CreatedAt), MAX(CreatedAt) FROM [dbo].[PurchaseOrders]
UNION ALL SELECT 'FinalInvoices',  COUNT(*), MIN(CreatedAt), MAX(CreatedAt) FROM [dbo].[FinalInvoices]
UNION ALL SELECT 'ShipmentNotes',  COUNT(*), MIN(CreatedAt), MAX(CreatedAt) FROM [dbo].[ShipmentNotes]
ORDER BY TableName;


/* ============================================================================
   B. EXECUTIVE SUMMARY — headline numbers for one date window
   ============================================================================ */

/* B1. One-row business summary.
   Grain: one row, period totals.
   SHAPE NOTE: every metric is its own scalar subquery over its own CTE.
   Nothing joins at the top level, so cross-table fan-out is impossible here.
   This is the mandatory shape whenever you return 2+ aggregates together. */
DECLARE @From   date = '2026-01-01';
DECLARE @ToExcl date = '2027-01-01';   -- day AFTER the last day you want

WITH Rfq AS (
    SELECT r.Id
    FROM [dbo].[RFQs] r
    WHERE r.CreatedAt >= @From AND r.CreatedAt < @ToExcl
),
SentQuotes AS (
    -- "sent" is a state machine: Accepted and Rejected were sent too.
    SELECT q.Id, q.RFQId, COALESCE(q.FinalPrice, q.TotalAmount) AS QuoteValue
    FROM [dbo].[Quotes] q
    WHERE q.CreatedAt >= @From AND q.CreatedAt < @ToExcl
      AND (q.SentAt IS NOT NULL OR q.Status IN ('Sent','Accepted','Rejected'))
),
SalesOrders AS (
    SELECT i.Id, i.TotalAmount
    FROM [dbo].[Invoices] i
    WHERE i.CreatedAt >= @From AND i.CreatedAt < @ToExcl
      AND i.IsCancelled = 0
),
Pos AS (
    SELECT po.Id, po.TotalAmount
    FROM [dbo].[PurchaseOrders] po
    WHERE po.CreatedAt >= @From AND po.CreatedAt < @ToExcl
      AND po.Status <> 'Cancelled'
),
Shipped AS (
    SELECT fi.Id, fi.TotalAmount
    FROM [dbo].[FinalInvoices] fi
    WHERE fi.CreatedAt >= @From AND fi.CreatedAt < @ToExcl
)
SELECT
    (SELECT COUNT(*)              FROM Rfq)                          AS RFQsReceived,
    (SELECT COUNT(DISTINCT RFQId) FROM SentQuotes)                   AS RFQsQuoted,
    (SELECT COUNT(*)              FROM SentQuotes)                   AS QuotesSent,
    (SELECT ISNULL(SUM(QuoteValue),0) FROM SentQuotes)               AS QuotedValue,
    (SELECT COUNT(*)              FROM SalesOrders)                  AS SalesOrders,
    (SELECT ISNULL(SUM(TotalAmount),0) FROM SalesOrders)             AS SalesOrderValue,
    (SELECT COUNT(*)              FROM Pos)                          AS PurchaseOrders,
    (SELECT ISNULL(SUM(TotalAmount),0) FROM Pos)                     AS PurchaseOrderValue,
    (SELECT COUNT(*)              FROM Shipped)                      AS FinalInvoices,
    (SELECT ISNULL(SUM(TotalAmount),0) FROM Shipped)                 AS FinalInvoiceValue,
    -- conversion rates: cast before dividing, guard the denominator
    CAST((SELECT COUNT(DISTINCT RFQId) FROM SentQuotes) AS decimal(18,4))
        / NULLIF((SELECT COUNT(*) FROM Rfq), 0)                      AS RFQtoQuoteRate,
    CAST((SELECT COUNT(*) FROM SalesOrders) AS decimal(18,4))
        / NULLIF((SELECT COUNT(*) FROM SentQuotes), 0)               AS QuoteToOrderRate;
GO


/* B2. Monthly pipeline funnel.
   Grain: one row per calendar month.
   Each stage is counted in its own CTE at month grain, then FULL-joined on the
   month key — so a month with POs but no RFQs still appears. */
DECLARE @From   date = '2026-01-01';
DECLARE @ToExcl date = '2027-01-01';

WITH Months AS (
    SELECT DATEFROMPARTS(YEAR(d), MONTH(d), 1) AS MonthStart
    FROM (
        SELECT CreatedAt AS d FROM [dbo].[RFQs]           WHERE CreatedAt >= @From AND CreatedAt < @ToExcl
        UNION ALL SELECT CreatedAt FROM [dbo].[Quotes]         WHERE CreatedAt >= @From AND CreatedAt < @ToExcl
        UNION ALL SELECT CreatedAt FROM [dbo].[Invoices]       WHERE CreatedAt >= @From AND CreatedAt < @ToExcl
        UNION ALL SELECT CreatedAt FROM [dbo].[PurchaseOrders] WHERE CreatedAt >= @From AND CreatedAt < @ToExcl
        UNION ALL SELECT CreatedAt FROM [dbo].[FinalInvoices]  WHERE CreatedAt >= @From AND CreatedAt < @ToExcl
    ) x
    GROUP BY DATEFROMPARTS(YEAR(d), MONTH(d), 1)
),
R AS (SELECT DATEFROMPARTS(YEAR(CreatedAt),MONTH(CreatedAt),1) m, COUNT(*) c
      FROM [dbo].[RFQs] WHERE CreatedAt >= @From AND CreatedAt < @ToExcl GROUP BY DATEFROMPARTS(YEAR(CreatedAt),MONTH(CreatedAt),1)),
Q AS (SELECT DATEFROMPARTS(YEAR(CreatedAt),MONTH(CreatedAt),1) m, COUNT(*) c,
             ISNULL(SUM(COALESCE(FinalPrice, TotalAmount)),0) v
      FROM [dbo].[Quotes]
      WHERE CreatedAt >= @From AND CreatedAt < @ToExcl
        AND (SentAt IS NOT NULL OR Status IN ('Sent','Accepted','Rejected'))
      GROUP BY DATEFROMPARTS(YEAR(CreatedAt),MONTH(CreatedAt),1)),
I AS (SELECT DATEFROMPARTS(YEAR(CreatedAt),MONTH(CreatedAt),1) m, COUNT(*) c, ISNULL(SUM(TotalAmount),0) v
      FROM [dbo].[Invoices] WHERE CreatedAt >= @From AND CreatedAt < @ToExcl AND IsCancelled = 0
      GROUP BY DATEFROMPARTS(YEAR(CreatedAt),MONTH(CreatedAt),1)),
P AS (SELECT DATEFROMPARTS(YEAR(CreatedAt),MONTH(CreatedAt),1) m, COUNT(*) c, ISNULL(SUM(TotalAmount),0) v
      FROM [dbo].[PurchaseOrders] WHERE CreatedAt >= @From AND CreatedAt < @ToExcl AND Status <> 'Cancelled'
      GROUP BY DATEFROMPARTS(YEAR(CreatedAt),MONTH(CreatedAt),1)),
F AS (SELECT DATEFROMPARTS(YEAR(CreatedAt),MONTH(CreatedAt),1) m, COUNT(*) c, ISNULL(SUM(TotalAmount),0) v
      FROM [dbo].[FinalInvoices] WHERE CreatedAt >= @From AND CreatedAt < @ToExcl
      GROUP BY DATEFROMPARTS(YEAR(CreatedAt),MONTH(CreatedAt),1))
SELECT
    m.MonthStart,
    ISNULL(R.c,0) AS RFQs,
    ISNULL(Q.c,0) AS QuotesSent,      ISNULL(Q.v,0) AS QuotedValue,
    ISNULL(I.c,0) AS SalesOrders,     ISNULL(I.v,0) AS SalesOrderValue,
    ISNULL(P.c,0) AS POs,             ISNULL(P.v,0) AS POValue,
    ISNULL(F.c,0) AS FinalInvoices,   ISNULL(F.v,0) AS FinalInvoiceValue
FROM Months m
LEFT JOIN R ON R.m = m.MonthStart
LEFT JOIN Q ON Q.m = m.MonthStart
LEFT JOIN I ON I.m = m.MonthStart
LEFT JOIN P ON P.m = m.MonthStart
LEFT JOIN F ON F.m = m.MonthStart
ORDER BY m.MonthStart;
GO


/* ============================================================================
   C. CUSTOMER SUMMARY
   ============================================================================ */

/* C1. Customer 360.
   Grain: one row per customer.
   Every child table is rolled up to CustomerId in its OWN CTE first. Joining
   RFQs + Quotes + Invoices raw would multiply each other's counts. */
DECLARE @From   date = '2026-01-01';
DECLARE @ToExcl date = '2027-01-01';

WITH R AS (
    SELECT CustomerId, COUNT(*) AS RFQCount
    FROM [dbo].[RFQs]
    WHERE CreatedAt >= @From AND CreatedAt < @ToExcl
    GROUP BY CustomerId
),
Q AS (
    SELECT CustomerId,
           COUNT(*) AS QuotesSent,
           COUNT(DISTINCT RFQId) AS RFQsQuoted,
           ISNULL(SUM(COALESCE(FinalPrice, TotalAmount)),0) AS QuotedValue
    FROM [dbo].[Quotes]
    WHERE CreatedAt >= @From AND CreatedAt < @ToExcl
      AND (SentAt IS NOT NULL OR Status IN ('Sent','Accepted','Rejected'))
    GROUP BY CustomerId
),
I AS (
    SELECT CustomerId,
           COUNT(*) AS SalesOrders,
           ISNULL(SUM(TotalAmount),0) AS OrderedValue,
           SUM(CASE WHEN Status = 'Finish' THEN 1 ELSE 0 END) AS OrdersFinished
    FROM [dbo].[Invoices]
    WHERE CreatedAt >= @From AND CreatedAt < @ToExcl AND IsCancelled = 0
    GROUP BY CustomerId
),
Paid AS (
    -- CustomerPayments hangs off Invoices, so roll it up through the invoice first.
    SELECT i.CustomerId, ISNULL(SUM(cp.Amount),0) AS AmountPaid
    FROM [dbo].[CustomerPayments] cp
    JOIN [dbo].[Invoices] i ON i.Id = cp.InvoiceId
    WHERE i.CreatedAt >= @From AND i.CreatedAt < @ToExcl AND i.IsCancelled = 0
    GROUP BY i.CustomerId
),
F AS (
    SELECT CustomerId, COUNT(*) AS FinalInvoices, ISNULL(SUM(TotalAmount),0) AS BilledValue
    FROM [dbo].[FinalInvoices]
    WHERE CreatedAt >= @From AND CreatedAt < @ToExcl
    GROUP BY CustomerId
)
SELECT TOP 1000
    c.Id                       AS CustomerId,
    c.Name                     AS Customer,
    c.CustomerCode,
    c.Base,
    ISNULL(R.RFQCount,0)       AS RFQs,
    ISNULL(Q.RFQsQuoted,0)     AS RFQsQuoted,
    ISNULL(Q.QuotesSent,0)     AS QuotesSent,
    ISNULL(Q.QuotedValue,0)    AS QuotedValue,
    ISNULL(I.SalesOrders,0)    AS SalesOrders,
    ISNULL(I.OrderedValue,0)   AS OrderedValue,
    ISNULL(F.FinalInvoices,0)  AS FinalInvoices,
    ISNULL(F.BilledValue,0)    AS BilledValue,
    ISNULL(Paid.AmountPaid,0)  AS AmountPaid,
    ISNULL(I.OrderedValue,0) - ISNULL(Paid.AmountPaid,0) AS OutstandingOnOrders,
    CAST(ISNULL(I.SalesOrders,0) AS decimal(18,4))
        / NULLIF(ISNULL(Q.QuotesSent,0), 0)              AS WinRate
FROM [dbo].[Customers] c
LEFT JOIN R    ON R.CustomerId    = c.Id
LEFT JOIN Q    ON Q.CustomerId    = c.Id
LEFT JOIN I    ON I.CustomerId    = c.Id
LEFT JOIN Paid ON Paid.CustomerId = c.Id
LEFT JOIN F    ON F.CustomerId    = c.Id
WHERE ISNULL(R.RFQCount,0) + ISNULL(Q.QuotesSent,0) + ISNULL(I.SalesOrders,0) > 0
ORDER BY ISNULL(I.OrderedValue,0) DESC, c.Name;
GO


/* C2. Customers scoped to one base/office.
   Customers.Base is NULL on older rows — fall back to the CustomerCode prefix.
   Grain: one row per customer. */
DECLARE @Base int = 5;

SELECT TOP 1000 c.Id, c.Name, c.CustomerCode, c.Base, c.Country, c.IsActive
FROM [dbo].[Customers] c
WHERE c.Base = @Base
   OR (c.Base IS NULL AND c.CustomerCode LIKE 'C' + CAST(@Base AS varchar(10)) + '%')
ORDER BY c.Name;
GO


/* ============================================================================
   D. MARGIN / PROFITABILITY  (the "Total-PN" report shape)
   ============================================================================ */

/* D1. Margin per sales-order line — sell price vs the chosen supplier cost.
   Grain: one row per InvoiceItem.
   The two cost sources are 1:N children, so both are collapsed with OUTER APPLY
   TOP 1 + a deterministic ORDER BY. A plain JOIN would multiply the sell side. */
DECLARE @From   date = '2026-01-01';
DECLARE @ToExcl date = '2027-01-01';

SELECT TOP 1000
    c.Name                                  AS Customer,
    i.InvoiceNumber                         AS SalesOrder,
    i.CreatedAt                             AS OrderDate,
    i.Status                                AS OrderStatus,
    COALESCE(pit.PartNumberName, pn.Name)   AS PartNumber,
    ii.Qty                                  AS Qty,
    ii.UnitPrice                            AS SellUnitPrice,
    ii.TotalPrice                           AS SellTotal,
    cost.SupplierName                       AS Supplier,
    cost.Price                              AS BuyUnitPrice,
    cost.Price * ii.Qty                     AS BuyTotal,
    (ii.UnitPrice - cost.Price) * ii.Qty    AS GrossMargin,
    CASE WHEN ii.UnitPrice > 0
         THEN (ii.UnitPrice - cost.Price) / NULLIF(ii.UnitPrice, 0)
    END                                     AS MarginPct,
    po.PONumber                             AS PONumber,
    ii.Id                                   AS InvoiceItemId   -- keeps every row traceable
FROM [dbo].[InvoiceItems] ii
JOIN      [dbo].[Invoices]         i   ON i.Id  = ii.InvoiceId
JOIN      [dbo].[Customers]        c   ON c.Id  = i.CustomerId
LEFT JOIN [dbo].[ProcurementItems] pit ON pit.SourceInvoiceItemId = ii.Id
LEFT JOIN [dbo].[PartNumbers]      pn  ON pn.Id = pit.PartNumberId
OUTER APPLY (
    SELECT TOP 1 psq.Price, psq.SupplierName
    FROM [dbo].[ProcurementSupplierQuotes] psq
    WHERE psq.ProcurementItemId = pit.Id AND psq.IsSelected = 1
    ORDER BY psq.Id DESC                       -- deterministic tiebreak, required
) cost
OUTER APPLY (
    SELECT TOP 1 poi.POId
    FROM [dbo].[POItems] poi
    WHERE poi.SourceProcurementItemId = pit.Id
    ORDER BY poi.Id DESC
) poi
LEFT JOIN [dbo].[PurchaseOrders] po ON po.Id = poi.POId
WHERE i.CreatedAt >= @From AND i.CreatedAt < @ToExcl
  AND i.IsCancelled = 0
ORDER BY i.InvoiceNumber, ii.Id;
GO


/* D2. Margin rolled up per sales order, with a header-vs-lines cross-check.
   Grain: one row per sales order.
   Report BOTH totals. Agreement is evidence the number is right; a gap of whole
   currency units (not cents) is a real finding worth surfacing, not hiding. */
DECLARE @From   date = '2026-01-01';
DECLARE @ToExcl date = '2027-01-01';

WITH Lines AS (
    SELECT
        ii.InvoiceId,
        ii.Id                                AS InvoiceItemId,
        ii.Qty,
        ii.TotalPrice                        AS SellTotal,
        cost.Price * ii.Qty                  AS BuyTotal
    FROM [dbo].[InvoiceItems] ii
    LEFT JOIN [dbo].[ProcurementItems] pit ON pit.SourceInvoiceItemId = ii.Id
    OUTER APPLY (
        SELECT TOP 1 psq.Price
        FROM [dbo].[ProcurementSupplierQuotes] psq
        WHERE psq.ProcurementItemId = pit.Id AND psq.IsSelected = 1
        ORDER BY psq.Id DESC
    ) cost
),
PerOrder AS (
    SELECT InvoiceId,
           COUNT(*)                    AS LineCount,
           ISNULL(SUM(SellTotal),0)    AS SellTotal_Lines,
           ISNULL(SUM(BuyTotal),0)     AS BuyTotal_Lines,
           SUM(CASE WHEN BuyTotal IS NULL THEN 1 ELSE 0 END) AS LinesWithNoCost
    FROM Lines
    GROUP BY InvoiceId
)
SELECT TOP 1000
    c.Name              AS Customer,
    i.InvoiceNumber     AS SalesOrder,
    i.CreatedAt         AS OrderDate,
    i.Status            AS OrderStatus,
    i.TotalAmount       AS SellTotal_Header,     -- authoritative
    p.SellTotal_Lines,                            -- cross-check
    i.TotalAmount - p.SellTotal_Lines AS HeaderVsLinesGap,
    p.BuyTotal_Lines    AS EstimatedCost,
    p.SellTotal_Lines - p.BuyTotal_Lines AS GrossMargin,
    CASE WHEN p.SellTotal_Lines > 0
         THEN (p.SellTotal_Lines - p.BuyTotal_Lines) / NULLIF(p.SellTotal_Lines,0)
    END                 AS MarginPct,
    p.LineCount,
    p.LinesWithNoCost   -- >0 means the margin is understated; do not hide this
FROM [dbo].[Invoices] i
JOIN [dbo].[Customers] c ON c.Id = i.CustomerId
LEFT JOIN PerOrder p     ON p.InvoiceId = i.Id
WHERE i.CreatedAt >= @From AND i.CreatedAt < @ToExcl
  AND i.IsCancelled = 0
ORDER BY (p.SellTotal_Lines - p.BuyTotal_Lines) DESC, i.InvoiceNumber;
GO


/* ============================================================================
   E. SUPPLIER SUMMARY
   ============================================================================ */

/* E1. Supplier spend and activity.
   Grain: one row per supplier.
   PO header amounts and PO line amounts live at different grains, so each is
   aggregated separately and then joined on SupplierId. */
DECLARE @From   date = '2026-01-01';
DECLARE @ToExcl date = '2027-01-01';

WITH PoHeaders AS (
    SELECT po.SupplierId,
           COUNT(*)                            AS POCount,
           ISNULL(SUM(po.TotalAmount),0)       AS POValue_Header,
           SUM(CASE WHEN po.AdminApproval = 'Approved' THEN 1 ELSE 0 END) AS POsApproved,
           SUM(CASE WHEN po.Status = 'Completed'       THEN 1 ELSE 0 END) AS POsCompleted,
           SUM(CASE WHEN po.ReturnedAt IS NOT NULL     THEN 1 ELSE 0 END) AS POsReturned
    FROM [dbo].[PurchaseOrders] po
    WHERE po.CreatedAt >= @From AND po.CreatedAt < @ToExcl
      AND po.Status <> 'Cancelled'
    GROUP BY po.SupplierId
),
PoLines AS (
    SELECT po.SupplierId,
           COUNT(*)                       AS LineCount,
           ISNULL(SUM(poi.TotalPrice),0)  AS POValue_Lines,
           ISNULL(SUM(poi.Qty),0)         AS UnitsOrdered
    FROM [dbo].[POItems] poi
    JOIN [dbo].[PurchaseOrders] po ON po.Id = poi.POId    -- INNER: we want lines that ARE on a PO
    WHERE po.CreatedAt >= @From AND po.CreatedAt < @ToExcl
      AND po.Status <> 'Cancelled'
    GROUP BY po.SupplierId
),
Offers AS (
    -- how competitive each supplier is at the sourcing stage
    SELECT psq.SupplierId,
           COUNT(*)                                              AS OffersMade,
           SUM(CASE WHEN psq.IsSelected = 1 THEN 1 ELSE 0 END)   AS OffersWon
    FROM [dbo].[ProcurementSupplierQuotes] psq
    WHERE psq.CreatedAt >= @From AND psq.CreatedAt < @ToExcl
      AND psq.SupplierId IS NOT NULL
    GROUP BY psq.SupplierId
)
SELECT TOP 1000
    s.Id                      AS SupplierId,
    s.Name                    AS Supplier,
    s.Status                  AS SupplierStatus,
    s.IsActive,
    ISNULL(h.POCount,0)       AS POs,
    ISNULL(h.POValue_Header,0) AS Spend_Header,
    ISNULL(l.POValue_Lines,0)  AS Spend_Lines,      -- cross-check against Spend_Header
    ISNULL(l.UnitsOrdered,0)  AS UnitsOrdered,
    ISNULL(h.POsApproved,0)   AS POsApproved,
    ISNULL(h.POsCompleted,0)  AS POsCompleted,
    ISNULL(h.POsReturned,0)   AS POsReturned,
    ISNULL(o.OffersMade,0)    AS OffersMade,
    ISNULL(o.OffersWon,0)     AS OffersWon,
    CAST(ISNULL(o.OffersWon,0) AS decimal(18,4))
        / NULLIF(ISNULL(o.OffersMade,0),0)          AS OfferWinRate
FROM [dbo].[Suppliers] s
LEFT JOIN PoHeaders h ON h.SupplierId = s.Id
LEFT JOIN PoLines   l ON l.SupplierId = s.Id
LEFT JOIN Offers    o ON o.SupplierId = s.Id
WHERE ISNULL(h.POCount,0) + ISNULL(o.OffersMade,0) > 0
ORDER BY ISNULL(h.POValue_Header,0) DESC, s.Name;
GO


/* E2. Price competition on a procurement item — every offer, cheapest first.
   Grain: one row per procurement item PER competing supplier offer.
   This fan-out is INTENTIONAL; the grain is stated so nobody sums it by mistake. */
SELECT TOP 1000
    c.Name              AS Customer,
    pit.RfqName         AS RFQName,
    pit.PartNumberName  AS PartNumber,
    pit.ItemStatus,
    psq.SupplierName    AS Supplier,
    psq.Price           AS OfferPrice,
    psq.Qty             AS OfferQty,
    psq.Condition,
    psq.LeadTime,
    psq.CertName        AS TraceCerts,
    psq.IsSelected,
    pit.QuoteUnitPrice  AS PriceQuotedToCustomer,
    psq.Price - pit.QuoteUnitPrice AS OfferVsQuoted,
    RANK() OVER (PARTITION BY pit.Id ORDER BY psq.Price ASC) AS PriceRank
FROM [dbo].[ProcurementItems] pit
JOIN [dbo].[ProcurementSupplierQuotes] psq ON psq.ProcurementItemId = pit.Id
JOIN [dbo].[Procurements] pr ON pr.Id = pit.ProcurementId
JOIN [dbo].[Invoices]     i  ON i.Id  = pr.InvoiceId
JOIN [dbo].[Customers]    c  ON c.Id  = i.CustomerId
WHERE pit.PartNumberName IN (N'<PN-1>', N'<PN-2>')     -- <- your part numbers
ORDER BY pit.RfqName, pit.PartNumberName, psq.Price;
GO


/* ============================================================================
   F. PART NUMBER SUMMARY
   ============================================================================ */

/* F1. Most-demanded parts across the whole pipeline.
   Grain: one row per part number.
   Each stage counts in its own CTE — RFQ lines and quote lines are different
   grains and must never be joined raw. */
DECLARE @From   date = '2026-01-01';
DECLARE @ToExcl date = '2027-01-01';

WITH Req AS (
    SELECT ri.PartNumberId,
           COUNT(*)                AS TimesRequested,
           ISNULL(SUM(ri.Qty),0)   AS QtyRequested
    FROM [dbo].[RFQItems] ri
    JOIN [dbo].[RFQs] r ON r.Id = ri.RFQId
    WHERE r.CreatedAt >= @From AND r.CreatedAt < @ToExcl
    GROUP BY ri.PartNumberId
),
Quoted AS (
    SELECT qi.PartNumberId,
           COUNT(*)                                    AS TimesQuoted,
           ISNULL(SUM(qi.Qty),0)                       AS QtyQuoted,
           ISNULL(SUM(qi.TotalPrice),0)                AS QuotedValue,
           AVG(qi.UnitPrice)                           AS AvgQuotedUnitPrice
    FROM [dbo].[QuoteItems] qi
    JOIN [dbo].[Quotes] q ON q.Id = qi.QuoteId
    WHERE q.CreatedAt >= @From AND q.CreatedAt < @ToExcl
      AND (q.SentAt IS NOT NULL OR q.Status IN ('Sent','Accepted','Rejected'))
      AND qi.PartNumberId IS NOT NULL
    GROUP BY qi.PartNumberId
),
Bought AS (
    SELECT poi.PartNumberId,
           COUNT(*)                     AS TimesPurchased,
           ISNULL(SUM(poi.Qty),0)       AS QtyPurchased,
           ISNULL(SUM(poi.TotalPrice),0) AS PurchaseValue,
           AVG(poi.UnitPrice)           AS AvgPurchaseUnitPrice
    FROM [dbo].[POItems] poi
    JOIN [dbo].[PurchaseOrders] po ON po.Id = poi.POId
    WHERE po.CreatedAt >= @From AND po.CreatedAt < @ToExcl
      AND po.Status <> 'Cancelled'
      AND poi.PartNumberId IS NOT NULL
    GROUP BY poi.PartNumberId
)
SELECT TOP 1000
    pn.Id                          AS PartNumberId,
    pn.Name                        AS PartNumber,
    pn.Description,
    ISNULL(Req.TimesRequested,0)   AS TimesRequested,
    ISNULL(Req.QtyRequested,0)     AS QtyRequested,
    ISNULL(Quoted.TimesQuoted,0)   AS TimesQuoted,
    ISNULL(Quoted.QtyQuoted,0)     AS QtyQuoted,
    ISNULL(Quoted.QuotedValue,0)   AS QuotedValue,
    Quoted.AvgQuotedUnitPrice,
    ISNULL(Bought.TimesPurchased,0) AS TimesPurchased,
    ISNULL(Bought.QtyPurchased,0)  AS QtyPurchased,
    ISNULL(Bought.PurchaseValue,0) AS PurchaseValue,
    Bought.AvgPurchaseUnitPrice,
    Quoted.AvgQuotedUnitPrice - Bought.AvgPurchaseUnitPrice AS AvgUnitSpread
FROM [dbo].[PartNumbers] pn
LEFT JOIN Req    ON Req.PartNumberId    = pn.Id
LEFT JOIN Quoted ON Quoted.PartNumberId = pn.Id
LEFT JOIN Bought ON Bought.PartNumberId = pn.Id
WHERE ISNULL(Req.TimesRequested,0) + ISNULL(Quoted.TimesQuoted,0) + ISNULL(Bought.TimesPurchased,0) > 0
ORDER BY ISNULL(Req.TimesRequested,0) DESC, pn.Name;
GO


/* F2. Full pipeline trace for ONE part number — RFQ all the way to final invoice.
   Grain: one row per RFQ line for that part.
   Every downstream hop is collapsed to at most one row with OUTER APPLY, so the
   row count equals the number of times the part was actually requested. */
DECLARE @PartNumber nvarchar(200) = N'<part number>';

SELECT TOP 1000
    pn.Name              AS PartNumber,
    cu.Name              AS Customer,
    r.Name               AS RFQName,
    r.Status             AS RFQStatus,
    r.CreatedAt          AS RFQDate,
    q.QuoteNumber,       q.Status AS QuoteStatus,   qi.UnitPrice AS QuotedUnitPrice,
    i.InvoiceNumber      AS SalesOrder,
    i.Status             AS SalesOrderStatus,       ii.UnitPrice AS SoldUnitPrice,
    pit.ItemStatus       AS ProcurementItemStatus,
    psq.SupplierName     AS ChosenSupplier,         psq.Price AS BuyUnitPrice,
    po.PONumber,         po.Status AS POStatus,
    tn.TrackNumber,      tn.Status AS TrackingStatus,
    sn.SNNumber          AS ShipmentNote,           sn.Status AS ShipmentStatus,
    fi.InvoiceNumber     AS FinalInvoice,           fi.Status AS FinalInvoiceStatus
FROM [dbo].[RFQItems] ri
JOIN [dbo].[PartNumbers] pn ON pn.Id = ri.PartNumberId
JOIN [dbo].[RFQs]        r  ON r.Id  = ri.RFQId
JOIN [dbo].[Customers]   cu ON cu.Id = r.CustomerId
OUTER APPLY (SELECT TOP 1 x.* FROM [dbo].[QuoteItems] x       WHERE x.RFQItemId = ri.Id            ORDER BY x.Id DESC) qi
LEFT JOIN [dbo].[Quotes] q ON q.Id = qi.QuoteId
OUTER APPLY (SELECT TOP 1 x.* FROM [dbo].[InvoiceItems] x     WHERE x.QuoteItemId = qi.Id          ORDER BY x.Id DESC) ii
LEFT JOIN [dbo].[Invoices] i ON i.Id = ii.InvoiceId
OUTER APPLY (SELECT TOP 1 x.* FROM [dbo].[ProcurementItems] x WHERE x.SourceInvoiceItemId = ii.Id  ORDER BY x.Id DESC) pit
OUTER APPLY (SELECT TOP 1 x.* FROM [dbo].[ProcurementSupplierQuotes] x
             WHERE x.ProcurementItemId = pit.Id AND x.IsSelected = 1                               ORDER BY x.Id DESC) psq
OUTER APPLY (SELECT TOP 1 x.* FROM [dbo].[POItems] x          WHERE x.SourceProcurementItemId = pit.Id ORDER BY x.Id DESC) poi
LEFT JOIN [dbo].[PurchaseOrders] po ON po.Id = poi.POId
OUTER APPLY (SELECT TOP 1 x.* FROM [dbo].[POItemTrackNumbers] x WHERE x.POItemId = poi.Id          ORDER BY x.Id DESC) tn
OUTER APPLY (SELECT TOP 1 x.* FROM [dbo].[ShipmentNoteTrackNumbers] x WHERE x.TrackNumberId = tn.Id ORDER BY x.Id DESC) snt
LEFT JOIN [dbo].[ShipmentNotes] sn ON sn.Id = snt.ShipmentNoteId
OUTER APPLY (SELECT TOP 1 x.* FROM [dbo].[FinalInvoiceItems] x WHERE x.InvoiceItemId = ii.Id       ORDER BY x.Id DESC) fii
LEFT JOIN [dbo].[FinalInvoices] fi ON fi.Id = fii.FinalInvoiceId
WHERE pn.Name = @PartNumber
ORDER BY r.CreatedAt DESC;
GO


/* ============================================================================
   G. OPEN WORK / ACTION CENTRE
   ============================================================================ */

/* G1. Everything currently waiting on someone.
   Grain: one row per work category. A single UNION ALL scoreboard. */
SELECT 'RFQs open'                  AS Category, COUNT(*) AS Cnt FROM [dbo].[RFQs]   WHERE Status IN ('Open','In Progress')
UNION ALL
SELECT 'RFQs waiting for admin',    COUNT(*) FROM [dbo].[RFQs]   WHERE Status = 'Waiting For Admin'
UNION ALL
SELECT 'RFQs past deadline, unresolved', COUNT(*) FROM [dbo].[RFQs]
    WHERE LeadTime < CAST(GETDATE() AS date) AND Status IN ('Open','In Progress','Waiting For Admin','Ready To Quote')
UNION ALL
SELECT 'Quotes draft (never sent)', COUNT(*) FROM [dbo].[Quotes] WHERE SentAt IS NULL AND Status = 'Draft'
UNION ALL
SELECT 'Sales orders running',      COUNT(*) FROM [dbo].[Invoices] WHERE Status = 'Running' AND IsCancelled = 0
UNION ALL
SELECT 'Procurement groups open',   COUNT(*) FROM [dbo].[Procurements] WHERE Status = 'Open'
UNION ALL
SELECT 'POs awaiting admin approval', COUNT(*) FROM [dbo].[PurchaseOrders] WHERE AdminApproval = 'Pending'
UNION ALL
SELECT 'POs awaiting payment',      COUNT(*) FROM [dbo].[PurchaseOrders] WHERE Status = 'Waiting For Payment'
UNION ALL
SELECT 'POs returned',              COUNT(*) FROM [dbo].[PurchaseOrders] WHERE ReturnedAt IS NOT NULL
UNION ALL
SELECT 'Track numbers in transit',  COUNT(*) FROM [dbo].[POItemTrackNumbers] WHERE Status = 'Ship to Warehouse'
UNION ALL
SELECT 'Received items pending review', COUNT(*) FROM [dbo].[TrackNumberItems] WHERE Status = 'Pending'
UNION ALL
SELECT 'Shipment notes not confirmed', COUNT(*) FROM [dbo].[ShipmentNotes] WHERE Status <> 'Confirmed'
UNION ALL
SELECT 'Suppliers pending approval', COUNT(*) FROM [dbo].[Suppliers] WHERE Status = 'Pending'
UNION ALL
SELECT 'Wallet transfers pending',  COUNT(*) FROM [dbo].[WalletTransferPendings] WHERE Status = 'Pending'
UNION ALL
SELECT 'Tasks not done',            COUNT(*) FROM [dbo].[Tasks] WHERE Status <> 2   -- int enum, not a string
ORDER BY Cnt DESC;
GO


/* G2. RFQs that never received a sent quote.
   Grain: one row per RFQ.
   NOT EXISTS, never NOT IN — one NULL in a NOT IN subquery returns zero rows. */
DECLARE @From   date = '2026-01-01';
DECLARE @ToExcl date = '2027-01-01';

SELECT TOP 1000
    r.Id, r.Name AS RFQName, c.Name AS Customer, r.Status, r.CreatedAt, r.LeadTime AS Deadline,
    u.Name AS AssignedExpert,
    DATEDIFF(day, r.CreatedAt, GETDATE()) AS AgeDays
FROM [dbo].[RFQs] r
JOIN      [dbo].[Customers] c ON c.Id = r.CustomerId
LEFT JOIN [dbo].[Users]     u ON u.Id = r.UserId        -- UserId is nullable
WHERE r.CreatedAt >= @From AND r.CreatedAt < @ToExcl
  AND NOT EXISTS (
        SELECT 1 FROM [dbo].[Quotes] q
        WHERE q.RFQId = r.Id
          AND (q.SentAt IS NOT NULL OR q.Status IN ('Sent','Accepted','Rejected'))
  )
ORDER BY r.CreatedAt DESC;
GO


/* G3. PO status board with receiving progress.
   Grain: one row per PO.
   Tracking is 1:N two levels deep, so it is pre-aggregated to POId in a CTE.
   Joining it directly would multiply every PO row by its track-number count. */
WITH Recv AS (
    SELECT poi.POId,
           COUNT(DISTINCT tn.Id) AS TrackNumbers,
           SUM(CASE WHEN tn.Status = 'Received in Warehouse' THEN 1 ELSE 0 END) AS Received
    FROM [dbo].[POItems] poi
    JOIN [dbo].[POItemTrackNumbers] tn ON tn.POItemId = poi.Id
    WHERE poi.POId IS NOT NULL
    GROUP BY poi.POId
),
Lines AS (
    SELECT poi.POId, COUNT(*) AS LineCount, ISNULL(SUM(poi.TotalPrice),0) AS LineTotal
    FROM [dbo].[POItems] poi
    WHERE poi.POId IS NOT NULL
    GROUP BY poi.POId
)
SELECT TOP 1000
    po.PONumber,
    s.Name              AS Supplier,
    cu.Name             AS EndCustomer,          -- via the sales order, may be NULL
    po.Status,
    po.AdminApproval,
    po.PaymentStatus,
    po.TotalAmount      AS POTotal_Header,
    ISNULL(l.LineTotal,0) AS POTotal_Lines,      -- cross-check
    ISNULL(l.LineCount,0) AS Lines,
    ISNULL(rv.TrackNumbers,0) AS TrackNumbers,
    ISNULL(rv.Received,0)     AS Received,
    po.CreatedAt        AS PODate,
    DATEDIFF(day, po.CreatedAt, GETDATE()) AS AgeDays
FROM [dbo].[PurchaseOrders] po
JOIN      [dbo].[Suppliers] s  ON s.Id  = po.SupplierId
LEFT JOIN [dbo].[Invoices]  i  ON i.Id  = po.InvoiceId     -- NULLABLE: must be LEFT
LEFT JOIN [dbo].[Customers] cu ON cu.Id = i.CustomerId
LEFT JOIN Recv  rv ON rv.POId = po.Id
LEFT JOIN Lines l  ON l.POId  = po.Id
WHERE po.Status <> 'Cancelled'
ORDER BY po.CreatedAt DESC;
GO


/* ============================================================================
   H. MONEY — receivables, wallets, cash movement
   ============================================================================ */

/* H1. Accounts receivable / payment status per sales order.
   Grain: one row per sales order.
   CustomerPayments is 1:N, pre-aggregated to InvoiceId before joining. */
WITH Paid AS (
    SELECT InvoiceId, ISNULL(SUM(Amount),0) AS AmountPaid, COUNT(*) AS PaymentCount,
           MAX(CreatedAt) AS LastPaymentAt
    FROM [dbo].[CustomerPayments]
    GROUP BY InvoiceId
)
SELECT TOP 1000
    c.Name                        AS Customer,
    i.InvoiceNumber               AS SalesOrder,
    i.CustomerPONumber            AS CustomerPO,
    i.Status,
    i.CreatedAt                   AS OrderDate,
    i.DueDate,
    i.TotalAmount,
    ISNULL(p.AmountPaid,0)        AS AmountPaid,
    i.TotalAmount - ISNULL(p.AmountPaid,0) AS Outstanding,
    ISNULL(p.PaymentCount,0)      AS Payments,
    p.LastPaymentAt,
    CASE WHEN i.DueDate IS NOT NULL AND i.DueDate < GETDATE()
              AND i.TotalAmount - ISNULL(p.AmountPaid,0) > 0
         THEN DATEDIFF(day, i.DueDate, GETDATE()) END AS DaysOverdue
FROM [dbo].[Invoices] i
JOIN [dbo].[Customers] c ON c.Id = i.CustomerId
LEFT JOIN Paid p ON p.InvoiceId = i.Id
WHERE i.IsCancelled = 0
  AND i.TotalAmount - ISNULL(p.AmountPaid,0) > 0    -- drop this line for all orders
ORDER BY Outstanding DESC, i.CreatedAt DESC;
GO


/* H2. Wallet (PaymentBox) balances.
   Grain: one row per wallet.
   PaymentBoxes has NO Balance column — balance is derived. This mirrors
   PaymentBoxService exactly: SUM(Deposit x rate) - SUM(Withdraw x rate),
   where a NULL ExchangeRate means 1. Do not invent a different convention. */
WITH Tx AS (
    SELECT t.PaymentBoxId,
           SUM(CASE WHEN t.Type = 'Deposit'  THEN t.Amount * ISNULL(t.ExchangeRate,1) ELSE 0 END) AS TotalDeposit,
           SUM(CASE WHEN t.Type = 'Withdraw' THEN t.Amount * ISNULL(t.ExchangeRate,1) ELSE 0 END) AS TotalWithdraw,
           COUNT(*)       AS TxCount,
           MAX(t.CreatedAt) AS LastTxAt
    FROM [dbo].[PaymentTransactions] t
    GROUP BY t.PaymentBoxId
)
SELECT
    pb.Id            AS WalletId,
    pb.Name          AS Wallet,
    cp.Name          AS Company,
    pb.Currency,
    ISNULL(tx.TotalDeposit,0)  AS TotalDeposit,
    ISNULL(tx.TotalWithdraw,0) AS TotalWithdraw,
    ISNULL(tx.TotalDeposit,0) - ISNULL(tx.TotalWithdraw,0) AS Balance,
    ISNULL(tx.TxCount,0)       AS Transactions,
    tx.LastTxAt
FROM [dbo].[PaymentBoxes] pb
LEFT JOIN [dbo].[CompanyPresets] cp ON cp.Id = pb.CompanyPresetId
LEFT JOIN Tx tx ON tx.PaymentBoxId = pb.Id
ORDER BY pb.Currency, Balance DESC;
GO


/* H3. Cash movement by month and direction.
   Grain: one row per month per currency per direction.
   NOTE: never blend currencies into one total — Currency is in the GROUP BY. */
DECLARE @From   date = '2026-01-01';
DECLARE @ToExcl date = '2027-01-01';

SELECT
    DATEFROMPARTS(YEAR(t.CreatedAt), MONTH(t.CreatedAt), 1) AS MonthStart,
    pb.Currency,
    t.Type            AS Direction,        -- Deposit | Withdraw
    t.FromType, t.ToType,                  -- Customer | Wallet | Supplier
    COUNT(*)          AS TxCount,
    SUM(t.Amount * ISNULL(t.ExchangeRate,1)) AS Amount
FROM [dbo].[PaymentTransactions] t
JOIN [dbo].[PaymentBoxes] pb ON pb.Id = t.PaymentBoxId
WHERE t.CreatedAt >= @From AND t.CreatedAt < @ToExcl
GROUP BY DATEFROMPARTS(YEAR(t.CreatedAt), MONTH(t.CreatedAt), 1),
         pb.Currency, t.Type, t.FromType, t.ToType
ORDER BY MonthStart, pb.Currency, Direction;
GO


/* ============================================================================
   I. WAREHOUSE & SHIPPING
   ============================================================================ */

/* I1. Inventory in transit and on hand, per warehouse.
   Grain: one row per warehouse per tracking status. */
SELECT
    w.Name                 AS Warehouse,
    w.Type                 AS WarehouseType,
    tn.Status              AS TrackingStatus,
    COUNT(DISTINCT tn.Id)  AS TrackNumbers,
    COUNT(DISTINCT poi.Id) AS POItems,
    ISNULL(SUM(poi.Qty),0) AS Units,
    ISNULL(SUM(poi.TotalPrice),0) AS ValueAtCost
FROM [dbo].[POItemTrackNumbers] tn
JOIN      [dbo].[POItems]    poi ON poi.Id = tn.POItemId
LEFT JOIN [dbo].[Warehouses] w   ON w.Id   = tn.WarehouseId    -- nullable
GROUP BY w.Name, w.Type, tn.Status
ORDER BY w.Name, tn.Status;
GO


/* I2. Outbound shipment notes with contents.
   Grain: one row per shipment note.
   Track numbers are M:N via a junction (double fan-out) — pre-aggregated first. */
WITH Contents AS (
    SELECT snt.ShipmentNoteId,
           COUNT(DISTINCT snt.TrackNumberId) AS TrackNumbers,
           COUNT(DISTINCT tni.POItemId)      AS DistinctPOItems
    FROM [dbo].[ShipmentNoteTrackNumbers] snt
    LEFT JOIN [dbo].[TrackNumberItems] tni ON tni.TrackNumberId = snt.TrackNumberId
    GROUP BY snt.ShipmentNoteId
),
Boxes AS (
    SELECT ShipmentNoteId, COUNT(*) AS BoxCount, ISNULL(SUM(WeightKg),0) AS TotalWeightKg
    FROM [dbo].[ShipmentNoteBoxes]
    GROUP BY ShipmentNoteId
)
SELECT TOP 1000
    sn.SNNumber        AS ShipmentNote,
    sn.Status,
    sn.Type            AS Incoterm,       -- DDP | CPT
    w.Name             AS FromWarehouse,
    sn.Destination,
    sn.SONumber        AS SalesOrderRef,
    sn.AWBNumber,
    u.Name             AS CreatedBy,
    sn.CreatedAt,
    ISNULL(ct.TrackNumbers,0)   AS TrackNumbers,
    ISNULL(ct.DistinctPOItems,0) AS POItems,
    ISNULL(b.BoxCount,0)        AS Boxes,
    ISNULL(b.TotalWeightKg,0)   AS TotalWeightKg
FROM [dbo].[ShipmentNotes] sn
JOIN      [dbo].[Warehouses] w ON w.Id = sn.WarehouseId
LEFT JOIN [dbo].[Users]      u ON u.Id = sn.CreatedByUserId
LEFT JOIN Contents ct ON ct.ShipmentNoteId = sn.Id
LEFT JOIN Boxes    b  ON b.ShipmentNoteId  = sn.Id
ORDER BY sn.CreatedAt DESC;
GO


/* ============================================================================
   J. TEAM ACTIVITY
   ============================================================================ */

/* J1. Expert / user productivity.
   Grain: one row per user. Each metric aggregated separately at user grain. */
DECLARE @From   date = '2026-01-01';
DECLARE @ToExcl date = '2027-01-01';

WITH RfqAssigned AS (
    SELECT UserId, COUNT(*) AS RFQsAssigned,
           SUM(CASE WHEN Status IN ('Sent','Accepted') THEN 1 ELSE 0 END) AS RFQsQuoted,
           SUM(CASE WHEN Status = 'No Quote'           THEN 1 ELSE 0 END) AS RFQsNoQuote
    FROM [dbo].[RFQs]
    WHERE CreatedAt >= @From AND CreatedAt < @ToExcl AND UserId IS NOT NULL
    GROUP BY UserId
),
QuotesMade AS (
    SELECT UserId, COUNT(*) AS QuotesCreated,
           SUM(CASE WHEN SentAt IS NOT NULL OR Status IN ('Sent','Accepted','Rejected') THEN 1 ELSE 0 END) AS QuotesSent,
           SUM(CASE WHEN Status = 'Accepted' THEN 1 ELSE 0 END) AS QuotesAccepted,
           ISNULL(SUM(COALESCE(FinalPrice, TotalAmount)),0) AS QuotedValue
    FROM [dbo].[Quotes]
    WHERE CreatedAt >= @From AND CreatedAt < @ToExcl
    GROUP BY UserId
),
Sourcing AS (
    SELECT UserId, COUNT(*) AS SourcingRecords
    FROM [dbo].[Procument]
    WHERE CreatedAt >= @From AND CreatedAt < @ToExcl AND UserId IS NOT NULL
    GROUP BY UserId
)
SELECT
    u.Id, u.Name AS UserName, u.Role, u.IsActive,
    ISNULL(ra.RFQsAssigned,0)  AS RFQsAssigned,
    ISNULL(ra.RFQsQuoted,0)    AS RFQsQuoted,
    ISNULL(ra.RFQsNoQuote,0)   AS RFQsNoQuote,
    ISNULL(qm.QuotesCreated,0) AS QuotesCreated,
    ISNULL(qm.QuotesSent,0)    AS QuotesSent,
    ISNULL(qm.QuotesAccepted,0) AS QuotesAccepted,
    ISNULL(qm.QuotedValue,0)   AS QuotedValue,
    ISNULL(sc.SourcingRecords,0) AS SourcingRecords,
    CAST(ISNULL(qm.QuotesAccepted,0) AS decimal(18,4))
        / NULLIF(ISNULL(qm.QuotesSent,0),0) AS AcceptanceRate
FROM [dbo].[Users] u
LEFT JOIN RfqAssigned ra ON ra.UserId = u.Id
LEFT JOIN QuotesMade  qm ON qm.UserId = u.Id
LEFT JOIN Sourcing    sc ON sc.UserId = u.Id
WHERE ISNULL(ra.RFQsAssigned,0) + ISNULL(qm.QuotesCreated,0) + ISNULL(sc.SourcingRecords,0) > 0
ORDER BY ISNULL(qm.QuotesSent,0) DESC, u.Name;
GO


/* ============================================================================
   K. DATA-QUALITY CHECKS — run before trusting any report
   ============================================================================ */

/* K1. Orphan / gap detector.
   Grain: one row per check. Nonzero counts mean a join you assumed is incomplete. */
SELECT 'POItems with no PO'                    AS Check_, COUNT(*) AS Cnt FROM [dbo].[POItems] WHERE POId IS NULL
UNION ALL
SELECT 'POItems with no source procurement item', COUNT(*) FROM [dbo].[POItems] WHERE SourceProcurementItemId IS NULL
UNION ALL
SELECT 'PurchaseOrders not tied to a sales order', COUNT(*) FROM [dbo].[PurchaseOrders] WHERE InvoiceId IS NULL
UNION ALL
SELECT 'ProcurementItems with no selected supplier offer', COUNT(*)
    FROM [dbo].[ProcurementItems] pit
    WHERE NOT EXISTS (SELECT 1 FROM [dbo].[ProcurementSupplierQuotes] psq
                      WHERE psq.ProcurementItemId = pit.Id AND psq.IsSelected = 1)
UNION ALL
SELECT 'InvoiceItems with no procurement item', COUNT(*)
    FROM [dbo].[InvoiceItems] ii
    WHERE NOT EXISTS (SELECT 1 FROM [dbo].[ProcurementItems] pit WHERE pit.SourceInvoiceItemId = ii.Id)
UNION ALL
SELECT 'Customers with NULL Base', COUNT(*) FROM [dbo].[Customers] WHERE Base IS NULL
UNION ALL
SELECT 'Invoices flagged cancelled', COUNT(*) FROM [dbo].[Invoices] WHERE IsCancelled = 1
UNION ALL
SELECT 'Invoices where IsCancelled and Status disagree', COUNT(*)
    FROM [dbo].[Invoices] WHERE (IsCancelled = 1 AND Status <> 'Cancelled') OR (IsCancelled = 0 AND Status = 'Cancelled')
UNION ALL
SELECT 'Quotes flagged as Yuan (mixed currency risk)', COUNT(*)
    FROM [dbo].[Quotes] WHERE CoefYuan IS NOT NULL OR ExchangeRateYuan IS NOT NULL
ORDER BY Cnt DESC;
GO


/* K2. Currency guard — run before reporting ANY multi-quote total.
   If YuanFlagged > 0, a single blended total is meaningless; report by currency. */
DECLARE @From   date = '2026-01-01';
DECLARE @ToExcl date = '2027-01-01';

SELECT COUNT(*) AS QuotesInScope,
       SUM(CASE WHEN q.ExchangeRateYuan IS NOT NULL OR q.CoefYuan IS NOT NULL THEN 1 ELSE 0 END) AS YuanFlagged
FROM [dbo].[Quotes] q
WHERE q.CreatedAt >= @From AND q.CreatedAt < @ToExcl
  AND (q.SentAt IS NOT NULL OR q.Status IN ('Sent','Accepted','Rejected'));
GO
