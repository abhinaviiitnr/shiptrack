# SQL Optimization & Indexing Benchmark

This benchmark uses PostgreSQL 16 and the Orders service read patterns.

## 1. Dataset

The development-only `--seed-large` command uses PostgreSQL binary `COPY` to
create 300,000 orders and one item per order. The database used for this run
contained 300,001 orders and 300,001 order items because it also retained one
order from the Gateway smoke test.

The seed command is intentionally deterministic for customer IDs and dates:

```bash
docker compose run --rm --no-deps orders --seed-large
```

## 2. Admin order query

This matches the admin list: filter by status and date range, sort newest
first, and return one page.

```sql
SELECT "Id", "CustomerId", "Status", "ShippingMethod", "TotalAmount", "CreatedAt"
FROM "Orders"
WHERE "Status" = 1
  AND "CreatedAt" >= TIMESTAMPTZ '2026-01-01'
  AND "CreatedAt" <= TIMESTAMPTZ '2026-06-01'
ORDER BY "CreatedAt" DESC, "Id" DESC
LIMIT 20;
```

### Before the composite index

The indexes were dropped and the table was analyzed before this run:

```text
Limit (cost=7105.99..7108.33 rows=20 width=53)
  -> Gather Merge (cost=7105.99..8767.44 rows=14240 width=53)
       Workers Planned: 2
       Workers Launched: 2
       -> Sort
            Sort Key: "CreatedAt" DESC, "Id" DESC
            Sort Method: top-N heapsort
            -> Parallel Seq Scan on "Orders"
                 Rows Removed by Filter: 94163
                 Buffers: shared hit=3729
Planning Time: 1.654 ms
Execution Time: 13.462 ms
```

### After the composite index

The migration [20261119080337_AddDescendingOrderIndexes.cs](../src/services/Orders.Api/Migrations/20261119080337_AddDescendingOrderIndexes.cs)
creates these indexes:

```sql
CREATE INDEX "IX_Orders_Status_CreatedAt"
ON "Orders" ("Status", "CreatedAt" DESC);
```

Measured plan:

```text
Limit (cost=1.28..19.15 rows=20 width=53)
  -> Incremental Sort (cost=1.28..16086.47 rows=18000 width=53)
       Presorted Key: "CreatedAt"
       -> Index Scan using "IX_Orders_Status_CreatedAt" on "Orders"
            Index Cond: (("Status" = 1)
              AND ("CreatedAt" >= '2026-01-01 00:00:00+00'::timestamp with time zone)
              AND ("CreatedAt" <= '2026-06-01 00:00:00+00'::timestamp with time zone))
            Buffers: shared hit=3 read=3
Planning Time: 0.908 ms
Execution Time: 0.157 ms
```

Execution time improved from **13.462 ms** to **0.157 ms**. PostgreSQL now
finds the requested status and date range through the index instead of scanning
all orders.

## 3. Customer order history query

```sql
SELECT "Id", "CustomerId", "Status", "ShippingMethod", "TotalAmount", "CreatedAt"
FROM "Orders"
WHERE "CustomerId" = 'a0000000-0000-0000-0000-000000000001'
ORDER BY "CreatedAt" DESC, "Id" DESC
LIMIT 20;
```

### Before the composite index

```text
Limit (cost=6294.80..6297.14 rows=20 width=53)
  -> Gather Merge (cost=6294.80..6323.50 rows=246 width=53)
       Workers Planned: 2
       Workers Launched: 2
       -> Sort
            Sort Key: "CreatedAt" DESC, "Id" DESC
            -> Parallel Seq Scan on "Orders"
                 Rows Removed by Filter: 99907
                 Buffers: shared hit=3729
Planning Time: 0.066 ms
Execution Time: 9.851 ms
```

### After the composite index

```sql
CREATE INDEX "IX_Orders_CustomerId_CreatedAt"
ON "Orders" ("CustomerId", "CreatedAt" DESC);
```

Measured plan:

```text
Limit (cost=4.33..82.89 rows=20 width=53)
  -> Incremental Sort (cost=4.33..1170.96 rows=297 width=53)
       Presorted Key: "CreatedAt"
       -> Index Scan using "IX_Orders_CustomerId_CreatedAt" on "Orders"
            Index Cond: ("CustomerId" =
              'a0000000-0000-0000-0000-000000000001'::uuid)
            Buffers: shared hit=21 read=3
Planning Time: 0.046 ms
Execution Time: 0.178 ms
```

Execution time improved from **9.851 ms** to **0.178 ms**.

## 4. Keyset pagination

Offset pagination gets slower as the page number grows because PostgreSQL must
read and discard every preceding row:

```sql
SELECT *
FROM "Orders"
ORDER BY "CreatedAt" DESC, "Id" DESC
OFFSET 10000
LIMIT 20;
```

The Orders repository uses a cursor containing the last order ID. It looks up
that order's `CreatedAt`, then asks PostgreSQL for rows after that `(CreatedAt,
Id)` position:

```sql
SELECT *
FROM "Orders"
WHERE "CreatedAt" < @CursorCreatedAt
   OR ("CreatedAt" = @CursorCreatedAt AND "Id" < @CursorId)
ORDER BY "CreatedAt" DESC, "Id" DESC
LIMIT 20;
```

This keeps page retrieval bounded by the page size rather than the page depth.
Reads also use `AsNoTracking()` and DTO projections to avoid change tracking
and unnecessary navigation-property loading.
