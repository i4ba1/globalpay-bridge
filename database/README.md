# Database assets

Schema evolution is owned by EF Core migrations in `src/GlobalPay.Infrastructure/Persistence/Migrations`.
This folder owns reviewed T-SQL that is not expressible (or not worth expressing) in migrations:
stored procedures and advanced index/tuning scripts.

## Layout

```
database/
  procedures/   reviewed stored procedures applied after migrations
  tuning/       index and statistics scripts backed by a measured workload
```

## Procedures

| Procedure | Purpose | Called from |
|-----------|---------|-------------|
| `usp_GetPayrollVariance` | Prior-period and per-worker variance report (windowed set-based query) | Reporting (R3) |
| `usp_GetMonthlyPayrollSummary` | Tenant/period aggregates with covering index support | Reporting (R3) |
| `usp_LeaseOutboxMessages` | Atomically lease pending outbox work with `UPDLOCK`/`READPAST` | Outbox dispatcher (production profile) |
| `usp_ReconcilePaymentResults` | Set-based matching of expected vs confirmed payments | Reconciliation worker (R3) |
| `usp_PurgeOperationalData` | Remove eligible transient data; never financial audit evidence | Scheduled retention job (R3) |

## Application order

1. `dotnet ef database update` (or the migration bundle) applies the schema.
2. Apply the procedures in `procedures/` with a reviewed script, in alphabetical order.

Procedures are idempotent (`CREATE OR ALTER`) and safe to re-apply on every deployment.

## Deliberate scope note

The API's variance and summary endpoints use EF Core projections so the portfolio profile runs against
any provider (including the in-memory provider used by tests). The procedures above are the set-based
production path mandated by `GBP-DBD-001` §5 and are the reference implementation for reporting scale
work in R3. Where both exist, the procedure is authoritative and a parity test must accompany any
switch of the endpoint to the procedure.
