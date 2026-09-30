# GlobalPay Bridge — Requirements Traceability Matrix

Document ID: GBP-RTM-001
Version: 1.2
Source requirements: GBP-PRD-001, GBP-FRD-001 (derived), GBP-API-001, GBP-DBD-001, GBP-ARC-001

## Purpose

Trace every product requirement to the functional requirements that realise it and to the automated
verification that proves it. A requirement is only "covered" when a named test exists and passes.

## Product requirement to functional requirement to verification

| PR | Requirement | Functional requirements | Phase | Verification (implemented) |
|----|-------------|-------------------------|-------|----------------------------|
| PR-01 | Isolate tenant data and authorization context | FR-TEN-01..06 | MVP | `TenantIsolationTests` (3), `RepositoryIntegrationTests.TenantFilter_IsolatesNewEntities`, `ApiPipelineTests` (401/403), `LayerDependencyTests` |
| PR-02 | Effective-dated worker/contract records | FR-WRK-01..06 | MVP | `ContractTests` (6), `WorkforceHandlerTests` (duplicate ref, overlap, not found), `RepositoryIntegrationTests.ContractRepository_ResolvesEffectiveContractForDate` |
| PR-03 | Upload and validate timesheet CSV | FR-TIM-01..06 | MVP | `TimesheetCsvParserTests` (6), `TimesheetImportHandlerTests` (valid, unknown worker, checksum mismatch, idempotent) |
| PR-04 | Deterministic payroll snapshots | FR-PAY-01..05 | MVP | `PayrollCalculatorTests` (6), `PayrollFlowTests` (happy path, determinism, imports required, tenant required) |
| PR-05 | Maker-checker approval | FR-PAY-06..08 | MVP | `PayrollRunTests` (7), `PayrollFlowTests.Approve_ByCreator_IsRejected`, `Decide_WithoutApproverRole_IsForbidden` |
| PR-06 | Idempotent payment batches | FR-PMT-01..04 | R1 | `PaymentsFlowTests` (idempotent retry, fingerprint mismatch, dispatch submit), `InMemoryIdempotencyStore` contract |
| PR-07 | Verify payment webhooks | FR-PMT-07..09 | R1 | `PaymentsFlowTests` (invalid signature, duplicate event, settlement, unknown reference) |
| PR-08 | Reconcile and expose exceptions | FR-REC-01..04 | R3 | `ReconciliationFlowTests` (6): case opened, idempotent, none when settled, resolve, double resolve, authorization |
| PR-09 | Payroll and variance reporting | FR-PAY-09, FR-AUD-05 | MVP/R3 | `PayrollReportingQueries` + `usp_GetPayrollVariance`; performance run scheduled in R3 |
| PR-10 | End-to-end auditability and telemetry | FR-AUD-01..04, FR-X-01 | MVP/R2 | `RepositoryIntegrationTests.AuditEventRepository_ReturnsNewestFirst_AndPagesByCursor`, `ApiSmokeTests.Response_Carries_Correlation_Id_Header`, OpenTelemetry wiring |
| PR-11 | Export payslip/summary artifacts | FR-AUD-06 | R3 | `IBlobStorage.CreateReadUrlAsync` time-limited URL; endpoint scheduled (see gaps) |
| PR-12 | Safe operational replay | FR-X-05 | R3 | Outbox replay procedure documented in `docs/runbooks/outbox-backlog.md`; automated test scheduled |

## Foundation coverage (Phase 0)

| Capability | Functional requirement | Verification |
|------------|------------------------|--------------|
| Tenant-scoped reads | FR-TEN-02..04 | `TenantIsolationTests` (3) |
| Money precision and currency rules | FR-X-06, FR-WRK-06 | `MoneyTests` (7), `CurrencyTests` (9) |
| Result and error contract | FR-X-01 | `ResultTests` (6) |
| Aggregate identity and domain events | FR-AUD-01 | `EntityTests` (3) |
| Clean Architecture boundaries | NFR-07 | `LayerDependencyTests` (7) |
| Host composition, health, OpenAPI | NFR-01, NFR-06, NFR-10 | `ApiSmokeTests` (4) |

## Non-functional requirements to verification

| NFR | Target | Verification approach | Status |
|-----|--------|-----------------------|--------|
| NFR-01 Availability | 99.9% monthly | Availability tests + App Insights | Wired; tests scheduled |
| NFR-02 Performance | P95 reads < 400 ms, commands < 750 ms | k6/NBomber load profile | Scheduled (R3) |
| NFR-03 Scale | 10,000 workers / 100,000 rows per period | Bulk import and calculation benchmark | Scheduled (R3) |
| NFR-04 Security | OWASP, least privilege, managed identity | Threat model + dependency scanning | `security-scan.yml` in place |
| NFR-05 Recovery | RPO <= 15 min, RTO <= 2 h | Point-in-time restore drill | Documented in migration runbook |
| NFR-06 Observability | Logs, metrics, traces, alerts | OpenTelemetry + Serilog wired | Wired; dashboards scheduled |
| NFR-07 Maintainability | Clean Architecture + architecture tests | `LayerDependencyTests` | Pass |
| NFR-08 Test quality | Critical branch >= 90%, overall >= 80% | coverlet gates | Suite green (115 tests); gate thresholds pending measurement |
| NFR-09 Audit | Immutable security and financial events | Append-only `AuditEvent` + audit rollups | Append-only in code; DB permission hardening scheduled |
| NFR-10 Accessibility | Machine-readable OpenAPI, usable errors | OpenAPI generation + problem-details tests | Pass |

## Test suite summary

| Project | Tests | Focus |
|---------|-------|-------|
| `GlobalPay.UnitTests` | 84 | Domain invariants, calculator determinism, maker-checker, handler use cases, payment idempotency, webhook rules, reconciliation |
| `GlobalPay.IntegrationTests` | 24 | CSV parser, repository behaviour with the in-memory provider, API pipeline (auth, authorization, validation, health, OpenAPI) |
| `GlobalPay.ArchitectureTests` | 7 | Layer and module boundary rules |
| **Total** | **115** | |

## Open traceability gaps

1. `GBP-FRD-001` and `GBP-QOP-001` were not supplied; FR and test identifiers here are derived.
   Reconcile when the source documents become available.
2. Payslip/summary export (PR-11) and automated outbox replay (PR-12) are implemented as ports and
   documented procedures; their endpoint and test coverage land with the R3 operational work.
3. SQL Server-specific behaviour (filtered indexes, stored procedures, `rowversion`) is covered by
   scripts and reviewed SQL but not yet by container-backed tests; `Testcontainers` is referenced and
   ready for that work (ADR-012).
4. Coverage gates (NFR-08) are not yet enforced in CI; thresholds must be measured before enabling.
