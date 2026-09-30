# GlobalPay Bridge — Derived Functional Requirements

Document ID: GBP-FRD-001 (derived)
Status: Derived baseline — replaces the source pack document that was not supplied
Derived from: GBP-PRD-001, GBP-API-001, GBP-DBD-001, GBP-ARC-001
Owner: Engineering
Version: 1.0

## Purpose

The source engineering pack references `GBP-FRD-001` for "detailed behavior", but that document was
not present in the repository. This baseline derives functional requirements from the documents that
**are** present and makes each derivation traceable. Every behaviour that is not explicitly stated in
a source document is marked **Assumed** so reviewers can challenge it rather than trust it.

Requirement IDs are stable and are referenced by tests and the traceability matrix.

## Conventions

- **ID** — module prefix + sequence: `FR-TEN`, `FR-WRK`, `FR-TIM`, `FR-PAY`, `FR-PMT`, `FR-REC`, `FR-AUD`.
- **Source** — `PR-nn` / `API-xxx-nn` / `DBD` / `ARC` for stated behaviour; `Assumed` for derived behaviour.
- **Priority** — Must / Should / Could, inherited from `PR-01`..`PR-12` where applicable.
- **Phase** — MVP, R1, R2, R3 (release plan, GBP-PRD-001 §8).

## 1. Tenancy and access (FR-TEN)

| ID | Requirement | Source | Priority | Phase |
|----|-------------|--------|----------|-------|
| FR-TEN-01 | Every authenticated request resolves a tenant from the identity token (`tenant_id` claim) into a request-scoped tenant context. | PR-01, ARC | Must | MVP |
| FR-TEN-02 | Every tenant-owned entity is stored with `TenantId`, and every read path is constrained to the current tenant. | PR-01, DBD §1 | Must | MVP |
| FR-TEN-03 | Cross-tenant reads and writes are impossible through any repository or query surface. | PR-01 | Must | MVP |
| FR-TEN-04 | Requests without a resolvable tenant are rejected for tenant-scoped operations; system operations may run unscoped. | Assumed (safety) | Must | MVP |
| FR-TEN-05 | Roles are enforced server-side: Admin, Approver, Finance, Auditor, IntegrationPartner, PlatformOperator. | PRD §3, API-001 §1 | Must | MVP |
| FR-TEN-06 | Authorization failures return 401/403 problem details with a stable `code`. | API-001 §1 | Must | MVP |

## 2. Workforce and contracts (FR-WRK)

| ID | Requirement | Source | Priority | Phase |
|----|-------------|--------|----------|-------|
| FR-WRK-01 | Create worker (POST /workers); `ExternalRef` is unique within a tenant. | API-WRK-01, DBD §2 | Must | MVP |
| FR-WRK-02 | Read worker by id (GET /workers/{id}), scoped to tenant. | API-WRK-02 | Must | MVP |
| FR-WRK-03 | Create effective-dated contract (POST /workers/{id}/contracts); date ranges may not overlap. | API-WRK-03, DBD §2 | Must | MVP |
| FR-WRK-04 | Compensation components (earnings, allowances, deductions, bonuses) are configured per contract. | PRD §4, DBD §3 | Must | MVP |
| FR-WRK-05 | Historical payroll results are unaffected by later contract changes (contract snapshot per calculation version). | PR-02 | Must | MVP |
| FR-WRK-06 | Currency on a contract must be in the ISO 4217 allow-list. | DBD §4, ADR-006 | Must | MVP |

## 3. Time import (FR-TIM)

| ID | Requirement | Source | Priority | Phase |
|----|-------------|--------|----------|-------|
| FR-TIM-01 | Register a timesheet import (POST /timesheet-imports) and return a scoped upload location. | API-IMP-01, ARC §5 | Must | MVP |
| FR-TIM-02 | Import is started by blob arrival and validated for checksum, schema, tenant ownership, and row values. | ARC §5 | Must | MVP |
| FR-TIM-03 | Invalid rows produce actionable row-level errors; valid rows import exactly once. | PR-03 | Must | MVP |
| FR-TIM-04 | Duplicate import content is rejected by `Unique (TenantId, Period, Sha256)`. | DBD §4 | Must | MVP |
| FR-TIM-05 | Read processing result (GET /timesheet-imports/{id}). | API-IMP-02 | Must | MVP |
| FR-TIM-06 | Import writes time entries, import result, outbox event, and audit record atomically. | ARC §5, DBD §7 | Must | MVP |

## 4. Payroll (FR-PAY)

| ID | Requirement | Source | Priority | Phase |
|----|-------------|--------|----------|-------|
| FR-PAY-01 | Create payroll run (POST /payroll-runs) with period, currency, and at least one completed import. | API-PAY-01, API-001 §4 | Must | MVP |
| FR-PAY-02 | Calculate a versioned snapshot (POST /payroll-runs/{id}/calculate); identical inputs produce identical output. | PR-04 | Must | MVP |
| FR-PAY-03 | Each calculation version records rule version and totals, and is immutable once submitted. | DBD §1, ADR-007 | Must | MVP |
| FR-PAY-04 | One result per worker per version: `Unique (PayrollRunVersionId, WorkerId)`. | DBD §4 | Must | MVP |
| FR-PAY-05 | Competing mutations are rejected via `RowVersion` / `If-Match` semantics. | API-001 §5 | Must | MVP |
| FR-PAY-06 | Submit for approval (POST /payroll-runs/{id}/submit). | API-PAY-03 | Must | MVP |
| FR-PAY-07 | Approve or reject (POST /payroll-runs/{id}/decisions); the creator cannot approve their own run. | PR-05, API-PAY-04 | Must | MVP |
| FR-PAY-08 | Responses expose `status`, `totals`, `calculationVersion`, and `allowedActions` for the current user. | API-001 §4 | Must | MVP |
| FR-PAY-09 | Variance report (GET /payroll-runs/{id}/variance) uses prior-period/windowed comparison. | API-PAY-05, DBD §5 | Should | MVP |

## 5. Payments (FR-PMT)

| ID | Requirement | Source | Priority | Phase |
|----|-------------|--------|----------|-------|
| FR-PMT-01 | Create and send a payment batch (POST /payroll-runs/{id}/payment-batches) as Finance. | API-PMT-01 | Must | R1 |
| FR-PMT-02 | Submission is idempotent: retry never creates a second instruction; unique key `(TenantId, OperationCode, IdempotencyKey)`. | PR-06, API-001 §5 | Must | R1 |
| FR-PMT-03 | The internal `paymentBatchId` is the provider idempotency key. | API-001 §5 | Must | R1 |
| FR-PMT-04 | Outbox delivery uses lease, bounded exponential backoff with jitter, and circuit breaking. | ARC §7 | Must | R1 |
| FR-PMT-05 | A timeout after send is treated as uncertain; provider status is queried before resubmission. | API-001 §7 | Must | R1 |
| FR-PMT-06 | Read payment status (GET /payment-batches/{id}). | API-PMT-02 | Must | R1 |
| FR-PMT-07 | Webhooks are verified: timestamp tolerance, constant-time HMAC over timestamp+period+raw body, unique event id. | PR-07, API-001 §6 | Must | R1 |
| FR-PMT-08 | Inbox insert precedes the state transition; duplicates are acknowledged without a second mutation. | API-001 §6, ARC §5 | Must | R1 |
| FR-PMT-09 | Only legal monotonic state transitions are applied, regardless of event ordering. | API-001 §6/§7 | Must | R1 |

## 6. Reconciliation (FR-REC)

| ID | Requirement | Source | Priority | Phase |
|----|-------------|--------|----------|-------|
| FR-REC-01 | Scheduled matching compares expected and confirmed results set-based. | PR-08, DBD §5 | Must | R3 |
| FR-REC-02 | Mismatches open a reconciliation case with reason, owner, state, and audit history. | PR-08 | Must | R3 |
| FR-REC-03 | List exceptions (GET /reconciliation-cases) for Finance/Auditor. | API-REC-01 | Must | R3 |
| FR-REC-04 | Resolve a case (POST /reconciliation-cases/{id}/resolution). | API-REC-02 | Must | R3 |
| FR-REC-05 | Automated matching achieves >= 99% on seeded data. | PRD §7 | Should | R3 |

## 7. Audit and reporting (FR-AUD)

| ID | Requirement | Source | Priority | Phase |
|----|-------------|--------|----------|-------|
| FR-AUD-01 | Security and financial events are recorded immutably and are not editable by application users. | PR-10, NFR-09 | Must | MVP |
| FR-AUD-02 | Audit records carry actor, action, target, timestamp, and correlation/trace id. | DBD §3 | Must | MVP |
| FR-AUD-03 | Search the audit timeline (GET /audit-events) as Auditor, read-only. | API-AUD-01 | Must | MVP |
| FR-AUD-04 | Telemetry links API request, function execution, SQL calls, and partner calls by correlation id. | PR-10 | Must | R2 |
| FR-AUD-05 | Payroll/variance reporting runs within the performance target. | PR-09, NFR-02 | Should | MVP/R3 |
| FR-AUD-06 | Payslip and payroll summary artifacts are retrievable by authorized users via time-limited links. | PR-11 | Should | R3 |

## 8. Cross-cutting behaviours (FR-X)

| ID | Requirement | Source | Priority | Phase |
|----|-------------|--------|----------|-------|
| FR-X-01 | Failures are returned as RFC 9457 problem details with stable `code` and `correlationId`. | API-001 §1/§4 | Must | MVP |
| FR-X-02 | Commands accept an `Idempotency-Key`; same fingerprint returns the stored result, different fingerprint returns 409. | API-001 §5 | Must | R1 |
| FR-X-03 | Collections use cursor pagination with a sort allow-list. | API-001 §1 | Should | MVP |
| FR-X-04 | OpenAPI is generated in CI and breaking changes must be reviewed. | API-001 §1/§8 | Should | R2 |
| FR-X-05 | Failed asynchronous items can be replayed without duplication. | PR-12 | Could | R3 |
| FR-X-06 | Money is `decimal(19,4)` with currency validation; timestamps are `datetime2` UTC. | DBD §1 | Must | MVP |

## Assumptions register

| ID | Assumption | Impact if wrong |
|----|-----------|-----------------|
| A-01 | `tenant_id` and `role` are emitted as top-level token claims by the identity provider. | Claim mapping in `TenantResolutionMiddleware` and `HttpCurrentUser` changes. |
| A-02 | Tax computation is deliberately non-statutory and driven by configurable demonstration rules. | Rule engine surface changes; outputs are never legally authoritative. |
| A-03 | Periods are calendar months unless a period boundary is supplied on the run. | Variance and summary grouping change. |
| A-04 | `ExternalRef` uniqueness is filtered to active workers. | Unique index filter changes. |
