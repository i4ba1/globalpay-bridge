# Runbook: webhook failures

## Symptom

- Provider reports non-2xx responses from `POST /api/v1/webhooks/payment-provider`.
- Payments stay `Submitted` or `Accepted` and never reach `Settled`.
- Alert: webhook rejection rate above threshold.

## Impact

Payment confirmation is delayed; reconciliation opens `MissingConfirmation` cases.

## Diagnosis

| Response | Meaning | Action |
|----------|---------|--------|
| 401 `webhook.signature_invalid` | Signature mismatch, or the timestamp is outside the tolerance window | Check the shared secret in Key Vault and the provider's clock skew |
| 400 `webhook.headers_missing` | `X-Provider-Event-Id` or `X-Provider-Signature` absent | Provider integration bug; raise with the provider |
| 400 `webhook.timestamp_invalid` | `X-Provider-Timestamp` is not a Unix timestamp in seconds | Provider integration bug |
| 400 `webhook.payload_invalid` | Body is not valid JSON or is incomplete | Provider integration bug; capture the raw body from the inbox |
| 400 `webhook.event_type_unknown` | Event type outside `payment.accepted`, `payment.failed`, `payment.settled` | Add support for the new event type, then replay |
| 404 `payments.batch_not_found` | No batch matches `paymentReference` | Usually ordering: the submission has not completed yet. The provider retries; do not insert into the inbox |

## Signature verification contract

`X-Provider-Signature` is a hex HMAC-SHA256 over `"{timestamp}.{rawBody}"` using the shared secret.
Comparison is constant-time. The receiver reads the **raw** body before any model binding, so do not add
middleware that rewrites the request body ahead of the endpoint.

## Mitigation

- Rotate the shared secret in Key Vault and both systems if a mismatch is caused by drift.
- For `payments.batch_not_found`, confirm the outbox dispatcher has submitted the batch, then ask the
  provider to redeliver.
- Duplicate events are acknowledged without a second mutation (unique `(ProviderCode, ProviderEventId)`
  in `ProviderEventInbox`), so redelivery is always safe.

## Verification

- `SELECT COUNT(*) FROM dbo.ProviderEventInbox WHERE ProcessedAtUtc IS NULL` returns to zero.
- The affected batch reaches `Settled` or `PartiallySettled`.
