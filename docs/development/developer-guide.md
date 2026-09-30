# GlobalPay Bridge — Developer Guide

How to build, run, test, and extend the platform on a developer machine.

Verified against: .NET SDK 10.0.401, Docker 29, on Windows with PowerShell. Commands use PowerShell
syntax; bash equivalents are noted where they differ.

---

## 1. Prerequisites

| Tool | Version | Why |
|------|---------|-----|
| .NET SDK | 10.0.x | `global.json` pins `10.0.401` with `rollForward: latestFeature` |
| Docker Desktop | any recent | Local profile: SQL Server, Redis, Azurite, Keycloak |
| Azure Functions Core Tools | v4 (optional) | Run the Functions host outside Docker |
| Git | any | — |

Verify:

```powershell
dotnet --list-sdks          # must include 10.0.x
docker version              # Docker Desktop engine must be running for the local profile
docker compose version
```

If `docker version` reports `cannot find the file` for
`npipe:////./pipe/dockerDesktopLinuxEngine`, Docker Desktop is installed but its Linux engine is not
running. Start Docker Desktop before using the local profile; everything else in this guide works
without it.

---

## 2. Repository orientation

```
src/GlobalPay.Domain         framework-free model + the payroll rule engine
src/GlobalPay.Application     use cases, ports, request validation
src/GlobalPay.Infrastructure  EF Core, Dapper, Blob, Redis, outbox/inbox, provider adapter
src/GlobalPay.Api             REST host, endpoints, composition root (Autofac)
src/GlobalPay.Functions       Blob import trigger, outbox dispatcher, reconciliation timer
src/GlobalPay.Contracts       request/response and integration event schemas
src/GlobalPay.ProviderSimulator  fake payment provider (local only)
tests/GlobalPay.UnitTests        domain + use-case rules (no infrastructure)
tests/GlobalPay.IntegrationTests CSV parser, repositories, API pipeline
tests/GlobalPay.ArchitectureTests layer/module boundary rules
database/procedures          T-SQL applied after migrations
infrastructure/bicep         Azure modules (R2)
docs/                        ADRs, derived requirements, traceability, runbooks
```

**Dependency rule (enforced by tests, not convention):**

```
Domain → nothing external
Application → Domain + Contracts + its own ports
Infrastructure → Application + Domain
Api / Functions → Application contracts + composition root
Contracts → primitives only
```

`tests/GlobalPay.ArchitectureTests` fails the build if any of these are violated. Run it after a
refactor.

---

## 3. First-time setup

```powershell
# 1. Restore local tools (dotnet-ef) from .config/dotnet-tools.json
dotnet tool restore

# 2. Restore packages and build
dotnet build GlobalPayBridge.slnx
```

The build treats **warnings as errors** and runs analyzers plus code-style rules. A successful build is
therefore guaranteed to be warning-free. If you see style or analyzer errors, fix them — do not add
`#pragma warning disable` without a recorded reason.

---

## 4. Running the tests

All projects (recommended):

```powershell
dotnet test GlobalPayBridge.slnx -m:1
```

`-m:1` runs test projects **sequentially**. This is required on a typical developer machine: three test
hosts starting in parallel can exhaust memory and fail with
`Failed to create CoreCLR, HRESULT: 0x8007000E`. CI uses the same flag.

Fast rerun after a code change (skip the build):

```powershell
dotnet test GlobalPayBridge.slnx --no-build -c Release -m:1
```

Expected result (measured):

| Project | Tests | Focus |
|---------|-------|-------|
| `GlobalPay.UnitTests` | 84 | Domain invariants, calculator determinism, maker-checker, handler use cases, payment idempotency, webhook rules, reconciliation |
| `GlobalPay.IntegrationTests` | 24 | CSV parser, repositories + tenant isolation, API pipeline (401/403/400/health/OpenAPI) |
| `GlobalPay.ArchitectureTests` | 7 | Layer and module boundary rules |
| **Total** | **115** | roughly 18 seconds with `--no-build` |

### Run one project

```powershell
dotnet test tests/GlobalPay.UnitTests
dotnet test tests/GlobalPay.IntegrationTests
dotnet test tests/GlobalPay.ArchitectureTests
```

### Run a subset

```powershell
# one class
dotnet test tests/GlobalPay.UnitTests --filter "FullyQualifiedName~PaymentsFlowTests"

# one test
dotnet test tests/GlobalPay.UnitTests --filter "Name=Webhook_SettlementMarksBatchSettled_AndRunPaid"

# everything mentioning the calculator
dotnet test tests/GlobalPay.UnitTests --filter "FullyQualifiedName~PayrollCalculator"
```

### See failure detail

```powershell
dotnet test tests/GlobalPay.IntegrationTests --filter "FullyQualifiedName~ApiPipelineTests" `
  --logger "console;verbosity=detailed"
```

### Coverage

```powershell
dotnet test GlobalPayBridge.slnx -m:1 --collect:"XPlat Code Coverage" --results-directory ./TestResults
```

Coverage is produced as Cobertura XML under `TestResults/`. Threshold gates (NFR-08: 90% critical
branch, 80% overall) are not yet enforced in CI — measure before enabling.

### If the test run fails oddly

```powershell
dotnet build-server shutdown     # clears MSBuild/VBCSCompiler servers that accumulate memory
```

`OutOfMemoryException` from NuGet or Roslyn analyzers during a build is almost always stale build
servers, not a code problem.

---

## 5. Local profile with Docker Compose (recommended)

Starts the full topology: API, Functions, SQL Server, Redis, Azurite, Keycloak, and the payment
provider simulator.

```powershell
docker compose up -d --build
docker compose ps
```

| Service | Endpoint | Notes |
|---------|----------|-------|
| API | http://localhost:5080 | Swagger UI `/swagger`, Scalar `/scalar/v1`, OpenAPI `/openapi/v1.json` |
| API health | `/health/live`, `/health/ready` | Liveness does not touch dependencies |
| Functions | http://localhost:7080 | `GET /api/health` |
| Provider simulator | http://localhost:5090 | `GET /health` |
| Keycloak | http://localhost:8080 | admin console `admin` / `Admin123!` |
| SQL Server | `localhost:11433` | `sa` / `GlobalPay_Local_2026!` |
| Redis | `localhost:6379` | |
| Azurite | `localhost:10000` | blob endpoint |

Local credentials are development-only and are never used outside this profile.

> **SQL Server is published on host port 11433, not 1433.** Many Windows machines already run a native
> SQL Server / Express instance on 1433, and whichever binds first wins — host connections would then
> silently reach that instance instead of the container, surfacing as
> `Login failed for user 'sa'` (error 18456). Publishing on 11433 avoids the clash. Inside the Compose
> network the containers still use `sqlserver,1433`.
>
> Also create the database before applying migrations: a connection string naming a database that does
> not exist is refused at login (state 38), so `dotnet ef database update` cannot bootstrap an empty
> server. Run `database/scripts/00-create-database.sql` once (see §8).

Wait for SQL Server to become healthy before using the API (the healthcheck takes up to ~40 s on first
run because the container initialises):

```powershell
docker compose ps sqlserver      # STATUS should become "healthy"
curl.exe -s http://localhost:5080/health/ready
```

### Apply the database schema

The API does not create the schema automatically. Apply migrations once the SQL container is healthy:

```powershell
dotnet tool restore
dotnet dotnet-ef database update `
  --project src/GlobalPay.Infrastructure `
  --startup-project src/GlobalPay.Infrastructure `
  --connection "Server=localhost,11433;Database=GlobalPayBridge;User Id=sa;Password=GlobalPay_Local_2026!;TrustServerCertificate=True;Encrypt=False"
```

Then apply the reviewed procedures (idempotent, safe to re-run):

```powershell
Get-ChildItem database/procedures/*.sql | Sort-Object Name | ForEach-Object {
  Write-Host "applying $($_.Name)"
  sqlcmd -S localhost,11433 -U sa -P 'GlobalPay_Local_2026!' -C -i $_.FullName
}
```

`sqlcmd` ships with the SQL Server container; if you do not have it locally, run the scripts through any
SQL client or `docker exec -i <sql-container> /opt/mssql-tools18/bin/sqlcmd ...`.

### Tear down

```powershell
docker compose down            # keep volumes (data survives)
docker compose down -v         # drop volumes (clean slate)
docker compose logs -f api     # follow one service
```

---

## 6. Running components without Docker

Useful for a fast inner loop when you already have SQL Server, Redis, and Azurite available, or when you
only need part of the stack. To debug under Visual Studio instead, see
[Running in Visual Studio](visual-studio.md).

### API

```powershell
dotnet run --project src/GlobalPay.Api
```
It listens on **http://localhost:5080** (from `src/GlobalPay.Api/Properties/launchSettings.json`) with
content root set to the project directory.

Interactive API documentation is generated from a single OpenAPI document:

| UI | URL |
|----|-----|
| Swagger UI | http://localhost:5080/swagger |
| Scalar | http://localhost:5080/scalar/v1 |
| OpenAPI document | http://localhost:5080/openapi/v1.json |

Both UIs carry a bearer (JWT) security scheme, so the **Authorize** button works: obtain a token
(§7), paste it into Authorize, and call any endpoint from the UI. Swagger UI persists the token across
page reloads for the session.

> **Content root matters.** Running the built executable directly from the repository root (for example
> `.\src\GlobalPay.Api\bin\Debug\net10.0\GlobalPay.Api.exe`) makes the process look for `appsettings.json`
> in the wrong directory and fail with `Blob storage is not configured`. Always launch with
> `dotnet run --project …` or set the working directory to the output folder.

### Provider simulator (required for the payment flow)

```powershell
dotnet run --project src/GlobalPay.ProviderSimulator   # http://localhost:5090
```

### Functions host

```powershell
cd src/GlobalPay.Functions
func start          # requires Azure Functions Core Tools v4
```

Configuration comes from `src/GlobalPay.Functions/local.settings.json`, which points at
`localhost` SQL/Redis/Azurite. Without Core Tools, run Functions through Docker Compose instead — it
uses the same configuration keys.

### Configuration for non-Docker runs

`src/GlobalPay.Api/appsettings.Development.json` contains local defaults for SQL, Redis, and Azurite.
Override them without editing tracked files by using user secrets (the project already has a
`UserSecretsId`):

```powershell
dotnet user-secrets set "ConnectionStrings:GlobalPay" `
  "Server=localhost,11433;Database=GlobalPayBridge;User Id=sa;Password=GlobalPay_Local_2026!;TrustServerCertificate=True;Encrypt=False" `
  --project src/GlobalPay.Api

dotnet user-secrets set "ConnectionStrings:Blobs" "UseDevelopmentStorage=true" --project src/GlobalPay.Api
dotnet user-secrets list --project src/GlobalPay.Api
```

Environment variables win over both, which is how Docker Compose configures the containers:

```powershell
$env:ConnectionStrings__GlobalPay = "Server=localhost,11433;Database=GlobalPayBridge;..."
dotnet run --project src/GlobalPay.Api
```

**Required at startup:** `ConnectionStrings:GlobalPay` (the SQL provider throws if it is missing) and
`ConnectionStrings:Blobs` (or `Blobs:ServiceUri`). Redis is only contacted by `/health/ready` and cache
usage, so the API starts without a reachable Redis.

---

## 7. End-to-end walkthrough

This exercises the whole happy path: worker → contract → import → calculation → maker-checker approval →
payment → webhook settlement. It assumes the Docker profile is up and the schema has been applied.

Set two shell variables for convenience:

```powershell
$api = "http://localhost:5080"
$kc  = "http://localhost:8080/realms/globalpay/protocol/openid-connect/token"

function Get-Token($user) {
  (curl.exe -s -X POST $kc `
    -d "grant_type=password&client_id=globalpay-cli&username=$user&password=Password123!" `
    | ConvertFrom-Json).access_token
}

$admin    = Get-Token "admin.user"
$approver = Get-Token "approver.user"
$finance  = Get-Token "finance.user"

function Call($method, $path, $body, $token) {
  $args = @("-s", "-X", $method, "$api$path", "-H", "Authorization: Bearer $token")
  if ($body) { $args += @("-H", "Content-Type: application/json", "-d", ($body | ConvertTo-Json -Depth 6 -Compress)) }
  curl.exe @args | ConvertFrom-Json
}
```

### 7.1 Create a worker and a contract

```powershell
$worker = Call POST "/api/v1/workers" @{ externalRef = "W-001"; displayName = "Ada Lovelace"; email = "ada@example.test" } $admin
$worker.id

$contract = Call POST "/api/v1/workers/$($worker.id)/contracts" @{
  startDate = "2026-01-01"
  endDate   = $null
  currency  = "USD"
  components = @(
    @{ type = "Earning";   code = "BASE"; rule = "HourlyRate";     amount = 25 },
    @{ type = "Deduction"; code = "TAX";  rule = "PercentOfGross"; amount = 10 }
  )
} $admin
$contract.components | Format-Table code, rule, amount
```

### 7.2 Register and upload a timesheet

```powershell
# Build the CSV and its checksum. The checksum must match what you register.
$csv = "workerRef,workDate,units`nW-001,2026-09-15,8`n"
$bytes = [System.Text.Encoding]::UTF8.GetBytes($csv)
$sha = [System.BitConverter]::ToString(
         [System.Security.Cryptography.SHA256]::HashData($bytes)).Replace("-","").ToLowerInvariant()

$registration = Call POST "/api/v1/timesheet-imports" @{
  periodStart = "2026-09-01"; periodEnd = "2026-09-30"
  fileName    = "timesheets.csv"; sha256 = $sha; sizeBytes = $bytes.Length
} $admin

# Upload to the returned time-limited URL (Azurite accepts shared-key SAS).
[System.IO.File]::WriteAllBytes("$PWD\timesheets.csv", $bytes)
curl.exe -s -X PUT -H "x-ms-blob-type: BlockBlob" --data-binary "@timesheets.csv" "$($registration.uploadUri)"

# The Functions host picks up the blob, validates, and imports.
Start-Sleep -Seconds 5
Call GET "/api/v1/timesheet-imports/$($registration.importId)" $null $admin |
  Format-List status, totalRows, validRows, invalidRows
```

`status` should be `Completed` with `validRows = 1`. Invalid rows are reported with codes and are also
written as a CSV error report next to the input blob.

### 7.3 Calculate, submit, approve

```powershell
$run = Call POST "/api/v1/payroll-runs" @{
  periodStart = "2026-09-01"; periodEnd = "2026-09-30"
  currency = "USD"; sourceImportIds = @($registration.importId); note = "September run"
} $admin

$calculated = Call POST "/api/v1/payroll-runs/$($run.id)/calculate" $null $admin
$calculated.totals | Format-List          # gross 200, deduction 20, net 180 for 8h at 25/h

Call POST "/api/v1/payroll-runs/$($run.id)/submit" $null $admin | Format-List status

# The creator cannot approve their own run — expect a 409 payroll.maker_checker_violation.
Call POST "/api/v1/payroll-runs/$($run.id)/decisions" @{ approve = $true } $admin

# A different user with the Approver role can.
Call POST "/api/v1/payroll-runs/$($run.id)/decisions" @{ approve = $true } $approver | Format-List status, decidedBy
```

### 7.4 Payment with idempotency

```powershell
$key = [guid]::NewGuid().ToString()
$batch = curl.exe -s -X POST "$api/api/v1/payroll-runs/$($run.id)/payment-batches" `
  -H "Authorization: Bearer $finance" -H "Idempotency-Key: $key" | ConvertFrom-Json
$batch | Format-List status, totalAmount

# Replaying with the same key returns the same batch and creates no second instruction.
$replay = curl.exe -s -X POST "$api/api/v1/payroll-runs/$($run.id)/payment-batches" `
  -H "Authorization: Bearer $finance" -H "Idempotency-Key: $key" | ConvertFrom-Json
"same batch: $($batch.id -eq $replay.id)"
```

The outbox dispatcher (a timer trigger, every 30 s) submits the batch to the provider simulator and the
batch moves to `Submitted`.

### 7.5 Simulate a settlement webhook

The webhook is HMAC-signed over `"{timestamp}.{rawBody}"` with `Payments:Provider:WebhookSecret`
(local default `local-dev-webhook-secret`).

```powershell
$reference = (Call GET "/api/v1/payment-batches/$($batch.id)" $null $finance).providerReference
$occurred  = (Get-Date).ToUniversalTime().ToString("o")
$payload   = @{ eventType = "payment.settled"; paymentReference = $reference; amount = 200; currency = "USD"; occurredAt = $occurred } |
             ConvertTo-Json -Compress

$ts  = [DateTimeOffset]::UtcNow.ToUnixTimeSeconds()
$hmac = [System.Security.Cryptography.HMACSHA256]::new(
          [System.Text.Encoding]::UTF8.GetBytes("local-dev-webhook-secret"))
$sig = [System.BitConverter]::ToString(
         $hmac.ComputeHash([System.Text.Encoding]::UTF8.GetBytes("$ts.$payload"))).Replace("-","").ToLowerInvariant()

curl.exe -s -X POST "$api/api/v1/webhooks/payment-provider" `
  -H "X-Provider-Event-Id: evt-$([guid]::NewGuid())" `
  -H "X-Provider-Timestamp: $ts" `
  -H "X-Provider-Signature: $sig" `
  -H "Content-Type: application/json" -d $payload

Call GET "/api/v1/payment-batches/$($batch.id)" $null $finance | Format-List status
Call GET "/api/v1/payroll-runs/$($run.id)" $null $admin | Format-List status
```

Expected: the batch becomes `Settled`, every instruction `Settled`, and the payroll run `Paid`.

A tampered signature returns `401 webhook.signature_invalid`; a replayed event id is acknowledged without
a second mutation (it is deduplicated in `ProviderEventInbox`).

### 7.6 Audit and reconciliation

```powershell
$auditor = Get-Token "auditor.user"
Call GET "/api/v1/audit-events?limit=20" $null $auditor | Select-Object -ExpandProperty items |
  Format-Table action, targetType, actor, occurredAtUtc

# Exceptions only appear for stuck or failed instructions.
Call GET "/api/v1/reconciliation-cases" $null $finance | Select-Object -ExpandProperty items |
  Format-Table reason, status, detail
```

### 7.7 Tenant isolation check

```powershell
$tenantB = Get-Token "tenant.b.admin"      # same roles, different tenant
Call GET "/api/v1/workers/$($worker.id)" $null $tenantB   # expect 404 worker.not_found
```

---

## 8. Database workflow

### Inspect the schema without a database

```powershell
dotnet dotnet-ef migrations script --idempotent `
  --project src/GlobalPay.Infrastructure `
  --startup-project src/GlobalPay.Infrastructure `
  --output schema.sql
```

### Add a migration after a model change

```powershell
dotnet dotnet-ef migrations add <DescriptiveName> `
  --project src/GlobalPay.Infrastructure `
  --startup-project src/GlobalPay.Infrastructure `
  --output-dir Persistence/Migrations
```

Then review the generated `Up`/`Down`. Migrations under `Migrations/` are excluded from analyzers via
`.editorconfig` (they are generated code).

### Roll back a migration

```powershell
dotnet dotnet-ef migrations remove --project src/GlobalPay.Infrastructure --startup-project src/GlobalPay.Infrastructure
```

See `docs/runbooks/database-migrations.md` for the expand → migrate → contract rules.

---

## 9. Making a change: the standard loop

1. **Domain first.** Add or change the invariant in `GlobalPay.Domain` and write a failing unit test.
   Domain types must not reference EF Core, Azure SDKs, ASP.NET, or `System.Text.Json`.
2. **Use case.** Add a command/query record plus its handler in
   `GlobalPay.Application/Modules/<Module>/…`. Return `Result`/`Result<T>`; never throw for expected
   outcomes (translate domain exceptions with `DomainExceptionMapping.ToError`).
3. **Ports.** If new persistence or an external dependency is needed, declare the interface in
   `GlobalPay.Application/Abstractions` and implement it in `GlobalPay.Infrastructure`.
4. **Contract.** Add the request/response records in `GlobalPay.Contracts`.
5. **Validation.** Add an `IRequestValidator<TRequest>` for the request contract; it is picked up by
   assembly scanning and executed by `ValidationEndpointFilter<TRequest>`.
6. **Endpoint.** Add it to the relevant `*Endpoints.cs` in `GlobalPay.Api`. Remember:
   - `[FromServices]` is **required** on handler parameters (Autofac does not expose
     `IServiceProviderIsService`, so minimal APIs cannot infer services),
   - apply the correct policy (`Policies.Admin` / `Approver` / `Finance` / `Auditor`),
   - return `result.Error.ToProblem()` for failures.
7. **Persistence.** Add an `IEntityTypeConfiguration<T>` under
   `GlobalPay.Infrastructure/Persistence/Configurations` and a repository under
   `.../Persistence/Repositories`. Use `decimal(19,4)` for money, `datetimeoffset(3)` for instants,
   `date` for dates, and `HasPrecision`/`HasMaxLength` everywhere.
8. **Migration.** Generate and review it (§8).
9. **Tests.** Unit-test the rule and the handler (see §10), then `dotnet test GlobalPayBridge.slnx -m:1`.
10. **Verify.** `dotnet build GlobalPayBridge.slnx -c Release` must be warning-free, and
    `dotnet test tests/GlobalPay.ArchitectureTests` must pass.

### Conventions enforced by the build

- File-scoped namespaces; braces always; explicit accessibility modifiers.
- Private fields prefixed with `_camelCase`; interfaces prefixed `I`; async methods suffixed `Async`.
- `System` usings first; no unused usings (they are build errors).
- Nullable reference types are enabled and warnings are errors.

---

## 10. Writing tests

**Unit tests (`tests/GlobalPay.UnitTests`)** — no database, no HTTP. They exercise domain types and
handlers against in-memory fakes.

The shared harness is `Support/PayrollHarness.cs`. It wires every fake and exposes handlers through their
interfaces, so a test reads like a use case:

```csharp
var harness = new PayrollHarness(Guid.NewGuid());
var runId = await Scenario.BuildApprovedRunAsync(harness);   // worker → contract → import → calculate → approve

var batch = await harness.CreateBatchHandler.HandleAsync(new CreatePaymentBatchCommand(runId, "key-1"));
Assert.True(batch.IsSuccess);
```

Fakes live in `Support/Fakes.cs`, `Support/PaymentsFakes.cs`, and
`Support/InMemoryReconciliationCaseRepository.cs`. Add a fake when a handler needs a new port; keep the
fake's behaviour obvious (no cleverness) so failures point at production code.

**Integration tests (`tests/GlobalPay.IntegrationTests`)** — three kinds:

- *Pure infrastructure logic* (e.g. `TimesheetCsvParserTests`) — instantiate the class directly.
- *Repository behaviour* (`RepositoryIntegrationTests`) — build a `GlobalPayDbContext` on the EF Core
  in-memory provider. Note the provider is case-sensitive where SQL Server is case-insensitive; do not
  assert collation behaviour here (ADR-012).
- *API pipeline* (`ApiSmokeTests`, `ApiPipelineTests`) — `ApiTestFactory` starts the real host and swaps
  in `TestAuthHandler`, which reads identity from headers (`X-Test-User`, `X-Test-Roles`,
  `X-Test-Tenant`). Use it to assert authentication, authorization, validation, health, and OpenAPI.

**Architecture tests** — `LayerDependencyTests` asserts the dependency rule and that the domain has no
framework dependencies. If a refactor breaks a boundary, fix the code rather than the test.

---

## 11. Troubleshooting

| Symptom | Cause | Fix |
|---------|-------|-----|
| `Blob storage is not configured` at startup | `ConnectionStrings:Blobs` missing, or the process was started from the wrong directory | Use `dotnet run --project src/GlobalPay.Api`, or set the connection string |
| `OutOfMemoryException` in NuGet or analyzers | Accumulated MSBuild/compiler servers | `dotnet build-server shutdown`, then rebuild |
| `Failed to create CoreCLR, HRESULT: 0x8007000E` during tests | Test hosts started in parallel | Add `-m:1` |
| `Globalization Invariant Mode is not supported` | `InvariantGlobalization` enabled | It must stay `false`; `Microsoft.Data.SqlClient` requires ICU (ADR-013) |
| Request to a protected endpoint returns 500 with `ICommandHandler… has not been registered` | Handler parameter missing `[FromServices]` | Annotate the parameter |
| 500 instead of 400 for a bad query string | `BadHttpRequestException` not mapped | Handled in `GlobalPayExceptionHandler`; keep it first in the switch |
| Import stays `Registered`/`Processing` | Blob trigger did not fire or checksum mismatched | Check `docker compose logs functions`; confirm the registered SHA-256 equals the uploaded file |
| 401 from the webhook | Signature or timestamp invalid | Recompute HMAC over `"{timestamp}.{rawBody}"`; check the shared secret and clock skew |
| `docker compose up` never becomes healthy | SQL Server still initialising | Wait ~40 s and check `docker compose ps` |
| Keycloak users missing | Realm import only runs on an empty data volume | `docker compose down -v` then `up` again |
| SQL login fails (`Login failed for user 'sa'`, error 18456) | A native SQL Server / Express instance owns host port 1433, so host connections never reach the container | Use `Server=localhost,11433` from the host (the Compose file publishes SQL Server on 11433 for this reason); containers keep using `sqlserver,1433` |
| `dotnet ef database update` fails at login on a fresh server | The connection string names a database that does not exist yet (state 38) | Run `database/scripts/00-create-database.sql` first (§8) |
| Port already in use | Another process owns a published port | Change the published port in `docker-compose.yml` or stop the other process |

Useful diagnostics:

```powershell
docker compose logs -f api
docker compose logs -f functions
dotnet user-secrets list --project src/GlobalPay.Api
Get-Process -Name GlobalPay.Api,testhost -ErrorAction SilentlyContinue | Select-Object Id, ProcessName
```

---

## 12. Continuous integration

| Workflow | Trigger | Purpose |
|----------|---------|---------|
| `.github/workflows/ci.yml` | push / PR | restore → Release build → tests with coverage → upload artifact |
| `.github/workflows/deploy.yml` | push to `main`, manual | build → Bicep deploy → migration bundle → staging slot + smoke test → swap → deploy Functions |
| `.github/workflows/openapi-diff.yml` | PR touching API/Contracts | blocks accidental breaking API changes via `oasdiff` |
| `.github/workflows/security-scan.yml` | push / PR / weekly | `dotnet list package --vulnerable` (fails on findings) |

Deployment uses OIDC federated credentials (`AZURE_CLIENT_ID`, `AZURE_TENANT_ID`,
`AZURE_SUBSCRIPTION_ID`) — no long-lived cloud secrets are stored. Required repository variables and
secrets are listed in the workflow files. Bicep is authored but has not yet been validated or deployed in
this repository (no Azure CLI available in the authoring environment).

---

## 13. Command cheat sheet

```powershell
dotnet tool restore                                   # dotnet-ef
dotnet build GlobalPayBridge.slnx                     # warning-free build required
dotnet test  GlobalPayBridge.slnx -m:1                # all 115 tests (sequential)
dotnet test  GlobalPayBridge.slnx --no-build -c Release -m:1
dotnet build-server shutdown                          # memory hygiene between runs

dotnet run --project src/GlobalPay.Api                 # http://localhost:5080, Swagger at /swagger
dotnet run --project src/GlobalPay.ProviderSimulator    # http://localhost:5090

docker compose up -d --build                           # full local topology
docker compose ps
docker compose down -v                                 # reset all local data

dotnet dotnet-ef migrations add <Name> --project src/GlobalPay.Infrastructure --startup-project src/GlobalPay.Infrastructure
dotnet dotnet-ef database update --project src/GlobalPay.Infrastructure --startup-project src/GlobalPay.Infrastructure
dotnet dotnet-ef migrations script --idempotent --project src/GlobalPay.Infrastructure --startup-project src/GlobalPay.Infrastructure --output schema.sql
```

Further reading: [README](../README.md) ·
[Running in Visual Studio](visual-studio.md) · [Swagger UI walkthrough](../walkthrough/README.md) ·
[ADRs](../adr/README.md) ·
[requirements & traceability](../requirements/traceability-matrix.md) ·
[runbooks](../runbooks/incident-triage.md) · [database assets](../../database/README.md)
