# Runbook: database migrations

## Principles

- Schema changes use the **expand → migrate → contract** pattern so an old and a new application
  version can run side by side during a rolling deployment (`GBP-DBD-001` §8).
- Migrations are applied by the migration bundle produced in CI, never by hand-authored DDL.
- Procedures in `database/procedures` are re-applied after migrations; they are `CREATE OR ALTER`.

## Forward deployment

1. CI builds `dotnet ef migrations bundle` from `src/GlobalPay.Infrastructure`.
2. The `migrate` job runs the bundle against the target connection string:

   ```bash
   ./efbundle --connection "$ConnectionStrings__GlobalPay"
   ```

3. The API and Functions deploy to staging, smoke tests run, then the slot is swapped.

## Expand / contract checklist

Before merging a schema change, confirm:

- [ ] New columns are nullable or have defaults so the previous version still inserts successfully.
- [ ] Dropped columns happen in a **later** release, after the new version has fully replaced the old.
- [ ] Renames are implemented as add-new, backfill, switch, then drop.
- [ ] Index creation uses `ONLINE = ON` where the edition supports it and the table is large.
- [ ] The change is backward compatible for one release.

## Rollback

1. Prefer deploying the previous application version (a code rollback) over reversing the schema.
2. If a schema reversal is required, generate a down migration and review it, then apply it:

   ```bash
   dotnet ef migrations script <FromMigration> <ToMigration> --output rollback.sql
   ```

3. Never run a down migration that drops a column holding financial data. Archive first, then contract.

## Verification

- `SELECT * FROM dbo.__EFMigrationsHistory ORDER BY MigrationId DESC` shows the expected latest migration.
- `/health/ready` returns `Healthy` after the swap.
- The smoke test in `deploy.yml` passes against the swapped slot.

## Recovery targets

`GBP-PRD-001` NFR-05 requires RPO ≤ 15 minutes and RTO ≤ 2 hours. Azure SQL point-in-time restore is the
primary mechanism; exercise the restore drill at least once per release train.
