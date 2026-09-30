# Runbook: incident triage

## Goal

Diagnose a seeded or production failure in under ten minutes using correlated telemetry
(`GBP-PRD-001` NFR-06, success measure "Mean diagnostic time < 10 minutes").

## Start from the correlation id

Every response carries `X-Correlation-Id`, and problem details echo it as `correlationId`. That value is
the join key across logs, traces, SQL calls, and partner calls.

1. Grab the `correlationId` from the failing response or the user's report.
2. In Application Insights, run:

   ```kusto
   union traces, requests, dependencies, exceptions
   | where customDimensions.correlationId == "<correlation-id>"
   | order by timestamp asc
   ```

3. Read the resulting timeline: request → tenant resolution → handler → SQL → outbox/provider.

## Triage by symptom

| Symptom | First check | Likely cause |
|---------|-------------|--------------|
| 500 on a command | `exceptions` for the correlation id | Unhandled fault; look for `server.error` |
| 409 with a stable `code` | The `code` value | Expected business rule (maker-checker, overlap, immutability) |
| 401/403 | Token claims | Missing `tenant_id`, wrong role, or expired token |
| 400 `request.validation_failed` | `errors` in problem details | Client payload; no server action |
| Import stuck in `Processing` | Functions logs for the import id | Blob trigger failure or checksum mismatch |
| Payment batch stuck in `Created` | Outbox backlog runbook | Dispatcher or provider issue |
| Batch `Submitted` with no settlement | Webhook failures runbook | Lost or rejected webhook |

## Command reference

```powershell
$token = (curl.exe -s -X POST "http://localhost:8080/realms/globalpay/protocol/openid-connect/token" `
  -d "grant_type=password&client_id=globalpay-cli&username=admin.user&password=Password123!" `
  | ConvertFrom-Json).access_token

curl.exe -s -H "Authorization: Bearer $token" http://localhost:5080/health/ready
curl.exe -s -H "Authorization: Bearer $token" "http://localhost:5080/api/v1/audit-events?limit=20"
```

## Escalation

- Financial state ambiguity (duplicate or missing payment) → treat as a financial incident: freeze the
  affected batch, do not replay blindly, and follow the outbox and webhook runbooks with a second pair
  of eyes.
- Data loss or corruption → follow the database migration runbook's restore procedure.
