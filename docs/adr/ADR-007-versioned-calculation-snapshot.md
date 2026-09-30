# ADR-007: Versioned calculation snapshots

- Status: Accepted
- Date: 2026-09-22
- Source: GBP-ARC-001 §8

## Context

Payroll results are financial records. Recalculation after a rule or contract change must not alter
what was previously approved and paid, and auditors must be able to reproduce any historical result.

## Decision

Persist each calculation as an immutable `PayrollRunVersion` capturing the rule version, input
identity, and totals, with one `WorkerPayment` row per worker. `PayrollRun` holds a transactional
pointer to the current version. Submitted versions are never mutated; corrections create a new version
or an explicit adjustment. Uniqueness is enforced by `(PayrollRunId, Version)` and
`(PayrollRunVersionId, WorkerId)`.

## Consequences

- Historical reproducibility and safe recalculation (PR-02, PR-04).
- Storage grows with each recalculation; retention policy is configuration by record class.
- Domain policy must reject in-place edits to submitted versions.
