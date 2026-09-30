# Changelog

All notable changes to this project are documented in this file.

The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and this project adheres to
[Semantic Versioning](https://semver.org/spec/v2.0.0.html).

This is a portfolio project. Payroll tax logic is deliberately non-statutory and driven by configurable
demonstration rules; nothing here moves real funds.

## [Unreleased]

### Added

- Documented Swagger UI walkthrough: 18 animated GIFs produced by headless Chromium
  (`docs/walkthrough/`), with a reusable Playwright harness in `tools/walkthrough/`.
- `docs/github/repository-and-release.md` describing repository metadata and the release steps.

## [0.1.0] - 2026-09-30

First tagged baseline: the complete engineering pack from foundation through release R3 as described in
`GlobalPay_Bridge_PRD_v1.0.pdf`.

### Added

**Foundation**

- Clean Architecture solution (`GlobalPayBridge.slnx`) with Domain, Application, Infrastructure,
  Contracts, Api, Functions and a payment provider simulator, plus unit, integration and architecture
  test projects.
- Build baseline: .NET 10 with `global.json` pinning, central package management, analyzers and code
  style enforced as errors, and an `.editorconfig` that excludes generated migrations.
- Domain primitives: `Entity`, `AggregateRoot`, `ValueObject`, `Money`/`Currency` (`decimal(19,4)`,
  banker's rounding, ISO 4217 allow-list), `Result`/`Error`, domain events and `Guard`.
- Multi-tenancy: request-scoped tenant context, EF Core global query filters bound to the context
  instance, and optional SQL row-level security as defence in depth.
- Cross-cutting: correlation IDs, RFC 9457 problem details with stable codes, OpenTelemetry, Serilog,
  health endpoints and rate limiting.
- Local profile: Docker Compose with SQL Server, Redis, Azurite, Keycloak, API, Functions and the
  provider simulator, plus a Keycloak realm with two tenants and six roles.

**MVP**

- Workforce: workers with tenant-unique external references, effective-dated contracts with an overlap
  invariant, and compensation components.
- Time import: scoped blob upload registration, checksum validation, streaming CSV parser with
  row-level errors, error-report artifacts, and a Functions blob trigger that imports exactly once.
- Payroll: deterministic gross-to-net rule engine, versioned immutable calculation snapshots, and a
  maker-checker approval workflow in which the creator cannot approve their own run.
- Reporting and audit: variance and monthly summary queries, an immutable audit timeline with cursor
  pagination, and generated OpenAPI.

**R1 — payments and integration**

- Payment batch and instruction aggregates with legal monotonic state transitions.
- Idempotent submission keyed by `(TenantId, OperationCode, IdempotencyKey)` with request fingerprints.
- Transactional outbox with leasing and bounded exponential backoff, dispatched by a timer-triggered
  Function.
- HMAC-SHA256 webhook verification over `"{timestamp}.{rawBody}"` with constant-time comparison,
  timestamp tolerance, and inbox deduplication on `(ProviderCode, ProviderEventId)`.

**R2 — Azure production profile**

- Bicep modules for App Service, Functions, Azure SQL, Storage, Redis, Key Vault, Application Insights
  and Log Analytics, with Key Vault secret references and managed-identity role assignments.
- GitHub Actions: CI (build and test), deploy (OIDC, migration bundle, staging slot with smoke test and
  swap), OpenAPI breaking-change diff, and dependency vulnerability scanning.

**R3 — operational excellence**

- Reconciliation cases with reason, owner, state and audit history, plus list and resolve endpoints.
- Five reviewed T-SQL procedures (variance, monthly summary, outbox lease, payment reconciliation,
  operational purge) and a guarded create-database script.
- Runbooks for outbox backlog, webhook failures, reconciliation exceptions, database migrations and
  incident triage.
- Thirteen architecture decision records and a derived functional-requirements baseline with a
  traceability matrix.

### Fixed

Defects found by the end-to-end Swagger walkthrough against the real Docker stack:

- Dockerfiles did not copy `.editorconfig`, so container builds lost the analyzer exclusion for
  generated migrations and failed.
- SQL Server was published on host port 1433, colliding with a local SQL Server Express instance;
  published on 11433 instead. A connection string naming a non-existent database is refused at login, so
  a guarded create-database script was added.
- Tokens obtained from the host carry a different issuer than the in-network Keycloak authority; an
  explicit issuer allow-list was added (signature, audience and lifetime validation stay enabled).
- `MapInboundClaims` rewrote the `role` claim to `ClaimTypes.Role`, so role policies never matched and
  real tokens could not authorize. Claim mapping is now disabled.
- The Keycloak realm omitted the `sub` and `preferred_username` claims because an imported realm does
  not receive Keycloak's built-in client scopes; both are now declared explicitly.
- `RequireAuthorization(A, B)` requires both policies, so three "either role" endpoints rejected every
  caller; composite policies were introduced.
- Payroll calculation ignored `sourceImportIds` and summed time from every import in the period; it is
  now scoped to the run's source imports.
- SAS upload URLs used the internal blob hostname and were unusable by clients; a public blob endpoint
  setting was introduced.
- The Functions container defaulted the payment provider address to `localhost`, so outbox dispatch
  could never reach the simulator; the in-network address is now configured.
- The `Idempotency-Key` header was read from the raw request and therefore invisible to OpenAPI;
  Swagger can now drive payment submission.

### Known limitations

- Payslip/summary export (PR-11) and automated outbox replay (PR-12) are implemented as ports and
  documented procedures; their endpoints and tests are not yet written.
- SQL Server-specific behaviour (filtered indexes, stored procedures, `rowversion`) is covered by
  scripts and reviewed SQL, not yet by container-backed tests.
- Coverage gates (NFR-08) are not enforced in CI.
- The Bicep templates are authored and validated for syntax only; they have not been deployed to a
  subscription.

[Unreleased]: https://github.com/i4ba1/globalpay-bridge/compare/v0.1.0...HEAD
[0.1.0]: https://github.com/i4ba1/globalpay-bridge/releases/tag/v0.1.0
