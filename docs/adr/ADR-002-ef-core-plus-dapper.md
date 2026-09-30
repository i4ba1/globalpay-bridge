# ADR-002: EF Core for aggregates, Dapper and stored procedures for reporting

- Status: Accepted
- Date: 2026-09-22
- Source: GBP-ARC-001 §8

## Context

Aggregate writes need change tracking, optimistic concurrency (`RowVersion`), and domain model
fidelity. Reporting and set-based calculation need explicit SQL, window functions, and predictable
query plans that an ORM does not express well.

## Decision

Use EF Core 10 for aggregate persistence and migrations. Use Dapper and reviewed T-SQL stored
procedures for set-based calculation and reporting (`usp_CalculatePayrollBatch`,
`usp_GetPayrollVariance`, `usp_GetMonthlyPayrollSummary`, `usp_LeaseOutboxMessages`,
`usp_ReconcilePaymentResults`).

## Consequences

- Domain logic stays in the domain; stored procedures are limited to reporting, set operations, and
  controlled batch boundaries (risk noted in GBP-PRD-001 §9).
- Two data-access styles must be kept consistent; the schema is owned by EF migrations plus reviewed
  SQL scripts for procedures and advanced indexes.
- Read models can be tuned independently of the write model.
