# GlobalPay Bridge

[![ci](https://github.com/i4ba1/globalpay-bridge/actions/workflows/ci.yml/badge.svg)](https://github.com/i4ba1/globalpay-bridge/actions/workflows/ci.yml)
[![release](https://img.shields.io/github/v/release/i4ba1/globalpay-bridge)](https://github.com/i4ba1/globalpay-bridge/releases)
[![license: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)
[![.NET 10](https://img.shields.io/badge/.NET-10.0-512BD4.svg)](https://dotnet.microsoft.com/)
[![tests: 115 passing](https://img.shields.io/badge/tests-115%20passing-brightgreen.svg)](tests)

An Azure-native, multi-tenant workforce and payroll integration platform. GlobalPay Bridge turns
approved time and compensation inputs into controlled payroll runs, idempotent payment instructions,
auditable status updates, and reconciliation results.

This repository is a portfolio-grade implementation of the GlobalPay Bridge engineering pack
(`GBP-PRD-001`, `GBP-ARC-001`, `GBP-DBD-001`, `GBP-API-001`; `GBP-FRD-001` derived — see
[docs/requirements](docs/requirements/functional-requirements.md)).

## Status

| Phase | Outcome | State |
|-------|---------|-------|
| Phase 0 | Foundation: solution, boundaries, persistence, identity, containers, tests, docs | Complete |
| Phase 1 (MVP) | Tenant/auth, workers and contracts, timesheet import, calculation, maker-checker approval, OpenAPI | Complete |
| Phase 2 (R1) | Payment adapter, webhook verification, retries, idempotency, audit evidence | Complete |
| Phase 3 (R2) | Azure production profile: Bicep, App Service, Functions, Key Vault, App Insights, CI/CD | Complete (infrastructure as code; not yet deployed to a subscription) |
| Phase 4 (R3) | Reconciliation, exports, replay, SQL procedures, runbooks | Complete for reconciliation, procedures and runbooks; export endpoint and replay test pending |

**Verification:** Release build with **0 warnings**; **115 tests pass** (84 unit, 24 integration, 7
architecture).

## Solution architecture

Clean Architecture with a dependency rule enforced by tests, not convention:

```
Domain          → nothing external (no EF Core, Azure SDK, ASP.NET, JSON)
Application     → Domain + Contracts + ports it owns
Infrastructure  → Application + Domain (adapters)
Api / Functions → Application contracts + composition root
Contracts       → stable primitives only
```

Business capabilities are modules: **Tenancy & Access, Workforce & Contracts, Time Import, Payroll,
Payments, Reconciliation, Audit & Reporting**. Modules never reference each other's implementations.

### Reliability patterns

- Transactional **outbox** for reliable publication; **inbox** with a unique provider event key for
  webhook deduplication (ADR-003).
- **Idempotency** keyed by `(TenantId, OperationCode, IdempotencyKey)` with a request fingerprint;
  replay returns the stored response, a different fingerprint returns 409.
- Optimistic concurrency via `RowVersion` / `If-Match`.
- Versioned, immutable payroll calculation snapshots (ADR-007).
- Bounded exponential backoff with jitter and circuit breaking on provider calls.

## API surface

| Method | Route | Role | Purpose |
|--------|-------|------|---------|
| POST | `/api/v1/workers` | Admin | Create a worker |
| GET | `/api/v1/workers/{id}` | authenticated | Read a worker |
| POST | `/api/v1/workers/{id}/contracts` | Admin | Create an effective-dated contract |
| POST | `/api/v1/timesheet-imports` | Admin | Register an import and get a scoped upload URL |
| GET | `/api/v1/timesheet-imports/{id}` | authenticated | Read the import result |
| POST | `/api/v1/payroll-runs` | Admin | Create a payroll run |
| GET | `/api/v1/payroll-runs/{id}` | authenticated | Read a run with its calculation snapshot |
| POST | `/api/v1/payroll-runs/{id}/calculate` | Admin | Calculate a deterministic snapshot |
| POST | `/api/v1/payroll-runs/{id}/submit` | Admin | Submit for approval |
| POST | `/api/v1/payroll-runs/{id}/decisions` | Approver | Approve or reject (maker-checker) |
| GET | `/api/v1/payroll-runs/{id}/variance` | Admin/Approver | Variance against the prior period |
| POST | `/api/v1/payroll-runs/{id}/payment-batches` | Finance | Create and queue a payment batch (`Idempotency-Key` required) |
| GET | `/api/v1/payment-batches/{id}` | Finance/Approver | Read payment status |
| POST | `/api/v1/webhooks/payment-provider` | provider signature | Receive a signed provider event |
| GET | `/api/v1/reconciliation-cases` | Finance/Auditor | List payment exceptions |
| POST | `/api/v1/reconciliation-cases/{id}/resolution` | Finance | Resolve or write off an exception |
| GET | `/api/v1/audit-events` | Auditor | Search the immutable audit timeline |

Failures use RFC 9457 problem details with a stable `code` and `correlationId`; validation adds
per-field `errors`.

Interactive documentation is served from the same generated OpenAPI document:

| UI | URL |
|----|-----|
| Swagger UI | http://localhost:5080/swagger |
| Scalar | http://localhost:5080/scalar/v1 |
| OpenAPI document | http://localhost:5080/openapi/v1.json |

Both UIs expose an **Authorize** control preconfigured with the bearer (JWT) scheme — paste a Keycloak or
Entra ID access token and call protected endpoints directly.

## Repository layout

```
src/GlobalPay.Domain            framework-free domain model and the payroll rule engine
src/GlobalPay.Application        use cases, ports, validation, messaging abstractions
src/GlobalPay.Infrastructure     EF Core, Dapper, Blob, Redis, outbox/inbox, provider adapter
src/GlobalPay.Api                REST host and composition root
src/GlobalPay.Functions          blob import, outbox dispatcher, scheduled reconciliation
src/GlobalPay.Contracts          external request/response and event schemas
src/GlobalPay.ProviderSimulator  payment provider simulator for local development
tests/…                          unit, integration and architecture suites
database/procedures              reviewed T-SQL (variance, summary, outbox lease, reconciliation, purge)
infrastructure/bicep             Azure modules, per-environment parameters
infrastructure/keycloak          local realm import for OIDC
docs/adr                         architecture decision records (13)
docs/requirements                derived functional requirements and traceability matrix
docs/runbooks                    outbox, webhooks, reconciliation, migrations, incident triage
```

## Prerequisites

- .NET SDK 10.0 (`global.json` pins 10.0.401 with `rollForward: latestFeature`)
- Docker Desktop for the local profile
- `dotnet-ef` (restored as a local tool via `dotnet tool restore`)

## Build and test

```powershell
dotnet build GlobalPayBridge.slnx
dotnet test  GlobalPayBridge.slnx -m:1
```

`-m:1` runs test projects sequentially; parallel test hosts can exhaust memory on a developer machine.
The build treats warnings as errors and runs analyzers plus code-style checks, so a green build means
zero warnings.

Migrations:

```powershell
dotnet tool restore
dotnet dotnet-ef migrations add <Name> --project src/GlobalPay.Infrastructure --startup-project src/GlobalPay.Infrastructure
```

## Local profile (Docker Compose)

```powershell
docker compose up -d --build
docker compose ps
```

| Service | URL / endpoint | Notes |
|---------|----------------|-------|
| API | http://localhost:5080 | Swagger UI at `/swagger`, Scalar at `/scalar/v1`, OpenAPI at `/openapi/v1.json` |
| API health | `/health/live`, `/health/ready` | Liveness does not touch dependencies |
| Functions | http://localhost:7080 | `GET /api/health` |
| Provider simulator | http://localhost:5090 | `GET /health` |
| Keycloak | http://localhost:8080 | `admin` / `Admin123!` |
| SQL Server | `localhost:11433` | `sa` / `GlobalPay_Local_2026!` |
| Redis | `localhost:6379` | |
| Azurite (blobs) | `localhost:10000` | |

Local credentials are development-only and are not used outside the Docker profile (ADR-005).

### Obtaining a token

The realm seeds two tenants and five users; the password for all seeded users is `Password123!`.

```powershell
$token = (curl.exe -s -X POST `
  "http://localhost:8080/realms/globalpay/protocol/openid-connect/token" `
  -d "grant_type=password&client_id=globalpay-cli&username=admin.user&password=Password123!" `
  | ConvertFrom-Json).access_token

curl.exe -s -H "Authorization: Bearer $token" http://localhost:5080/
```

| User | Role | Tenant |
|------|------|--------|
| `admin.user` | Admin | `1111…` |
| `approver.user` | Approver | `1111…` |
| `finance.user` | Finance | `1111…` |
| `auditor.user` | Auditor | `1111…` |
| `tenant.b.admin` | Admin | `2222…` (second tenant for isolation demos) |

### End-to-end demo

1. `POST /api/v1/workers` then `POST /api/v1/workers/{id}/contracts` (with a `BASE` hourly earning and a
   percentage deduction).
2. `POST /api/v1/timesheet-imports`, upload the CSV to the returned URL, and poll
   `GET /api/v1/timesheet-imports/{id}` until `Completed`. The Functions host processes the file.
3. `POST /api/v1/payroll-runs`, then `…/calculate`, `…/submit`, and finally `…/decisions` as
   `approver.user`. Approval by the creator is rejected by the maker-checker rule.
4. `POST /api/v1/payroll-runs/{id}/payment-batches` as `finance.user` with an `Idempotency-Key`. The
   outbox dispatcher submits to the provider simulator.
5. Post a signed `payment.settled` event to the webhook endpoint and observe the batch move to
   `Settled` and the payroll run to `Paid`.

## Configuration

The same configuration keys are used by the Docker profile and Azure, so behaviour does not change
between environments:

| Key | Purpose |
|-----|---------|
| `ConnectionStrings:GlobalPay` | System of record (SQL Server) |
| `ConnectionStrings:Redis` | Distributed cache |
| `ConnectionStrings:Blobs` | Blob storage (Azurite locally, Key Vault reference in Azure) |
| `Authentication:Authority` | OIDC authority (Keycloak locally, Entra ID in Azure) |
| `Authentication:Audience` | Expected token audience (`globalpay-api`) |
| `Payments:Provider:BaseAddress` | Payment provider base address |
| `Payments:Provider:WebhookSecret` | Shared secret for HMAC webhook verification |
| `Payments:Provider:WebhookToleranceSeconds` | Accepted clock skew for webhook timestamps |
| `ApplicationInsights:ConnectionString` | Optional; enables the Azure Monitor exporter |
| `Serilog:*` | Structured logging configuration |

## Documentation

- [Developer guide](docs/development/developer-guide.md) — build, run, test and extend the platform
- [Running in Visual Studio](docs/development/visual-studio.md) — debug the API from Visual Studio
- [Swagger UI walkthrough](docs/walkthrough/README.md) — the whole flow captured as animated GIFs
- [Repository and release](docs/github/repository-and-release.md) — name, description, topics, license, release
- [Functional requirements (derived)](docs/requirements/functional-requirements.md)
- [Traceability matrix](docs/requirements/traceability-matrix.md)
- [Architecture decision records](docs/adr/README.md)
- [Database assets and procedures](database/README.md)
- [Runbooks](docs/runbooks/incident-triage.md)
- [Changelog](CHANGELOG.md)

## License

[MIT](LICENSE) © 2026 GlobalPay Bridge

This is a portfolio project. Payroll tax computation is deliberately non-statutory and driven by
configurable demonstration rules, and the platform never moves real funds or connects to a production
bank.
