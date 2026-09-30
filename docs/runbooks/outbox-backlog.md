# Runbook: outbox backlog

## Symptom

- `OutboxMessage` rows remain unprocessed and `AvailableAtUtc` keeps moving forward.
- Payment batches stay in `Created` and never reach the provider.
- Alert: outbox pending count above threshold, or oldest pending age above threshold.

## Impact

Payment instructions are not submitted. Payroll runs remain `Approved` and are not marked `Paid`.

## Diagnosis

1. Confirm the Functions app is running and the `OutboxDispatcher` timer trigger is firing.
2. Inspect pending rows:

   ```sql
   SELECT TOP (50) Id, Type, Attempts, AvailableAtUtc, LeaseUntilUtc, LEFT(Error, 200) AS Error
   FROM dbo.OutboxMessage
   WHERE ProcessedAtUtc IS NULL
   ORDER BY AvailableAtUtc;
   ```

3. Read the `Error` column. `provider.unavailable` and `provider.timeout` are transient; `provider.http_4xx`
   is permanent and is quarantined as processed with an audit record instead of retrying.

## Mitigation

- **Transient provider outage:** confirm provider health, then wait for the backoff window (max 5 minutes
  between attempts). No action needed unless the outage exceeds the business cutoff.
- **Stuck lease:** a dispatcher crash can leave `LeaseUntilUtc` in the future. Leases expire after two
  minutes; verify the worker is healthy and let the lease expire.
- **Permanent rejection:** the batch is left in `Created` with an audit record `payments.batch.rejected`.
  Fix the payload, then create a new batch for the run (idempotency is keyed by run, so the original
  attempt must be abandoned first).

## Replay without duplication

Replay is safe because the provider submission uses the payment batch id as the idempotency key. To
redeliver a quarantined message, reset it:

```sql
UPDATE dbo.OutboxMessage
SET ProcessedAtUtc = NULL, LeaseUntilUtc = NULL, AvailableAtUtc = SYSDATETIMEOFFSET(), Attempts = 0
WHERE Id = @OutboxMessageId AND ProcessedAtUtc IS NOT NULL;
```

Never delete an outbox row: it is the delivery audit trail.

## Verification

- `SELECT COUNT(*) FROM dbo.OutboxMessage WHERE ProcessedAtUtc IS NULL` returns to zero.
- Affected batches show `Submitted` with a provider reference.
- A `payments.batch.submitted` audit event exists for each replayed batch.
