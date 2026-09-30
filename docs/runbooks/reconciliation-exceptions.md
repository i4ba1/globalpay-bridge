# Runbook: reconciliation exceptions

## Symptom

- `GET /api/v1/reconciliation-cases` returns open cases.
- Alert: open case count above threshold, or cases older than the close window.

## Impact

Payments are unconfirmed or failed; the payroll period cannot be closed cleanly.

## Case reasons

| Reason | Meaning | Typical cause |
|--------|---------|---------------|
| `MissingConfirmation` | Instruction is `Submitted` or `Accepted` with no settlement event | Provider delay, lost webhook, or provider outage |
| `PaymentFailed` | Instruction is `Failed` | Insufficient funds, invalid destination, provider rejection |
| `AmountMismatch` | Confirmed amount differs from expected | Partial payment or provider fee handling (reserved for R3+) |

## Diagnosis

1. List open cases for the tenant:

   ```sql
   SELECT Id, PaymentBatchId, PaymentInstructionId, Reason, Status, Owner, OpenedAtUtc, Detail
   FROM dbo.ReconciliationCase
   WHERE TenantId = @TenantId AND Status IN (0, 1)
   ORDER BY OpenedAtUtc;
   ```

2. Correlate with the batch: `GET /api/v1/payment-batches/{id}` and the `payments.webhook.applied`
   audit events for the same correlation id.
3. For `MissingConfirmation`, confirm the provider actually received the submission by reference.

## Mitigation

- **Provider delay:** leave the case open and re-run the reconciliation job after the provider's
  settlement window. Cases are idempotent per instruction.
- **Failed payment:** correct the destination or amount, then create a new payment batch for the affected
  workers. Never mutate a settled or failed instruction.
- **Write-off:** only with finance approval. `POST /api/v1/reconciliation-cases/{id}/resolution` with
  `writeOff: true` records the decision and the actor.

## Verification

- All cases for the period are `Resolved` or `WrittenOff`.
- Each closure has an audit event (`reconciliation.case.resolved` or `reconciliation.case.written_off`)
  carrying the actor and the note.
