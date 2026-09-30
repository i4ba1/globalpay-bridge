# ADR-001: Modular monolith first

- Status: Accepted
- Date: 2026-09-22
- Source: GBP-ARC-001 §8

## Context

The platform has seven business capabilities (Tenancy, Workforce, Time Import, Payroll, Payments,
Reconciliation, Audit/Reporting) with strong transactional consistency requirements across them —
notably payroll calculation, approval, and payment submission. Microservice decomposition would
introduce distributed transactions, eventual consistency, and operational overhead before any scaling
evidence exists.

## Decision

Build a single deployable modular monolith (`GlobalPay.Api`) plus one asynchronous worker project
(`GlobalPay.Functions`). Modules own their application contracts and schema objects. A module must not
reference another module's implementations; integration happens through published contracts and
integration events.

## Consequences

- Transactional consistency for payroll is straightforward (single database, single transaction).
- Architecture tests enforce module and layer boundaries so extraction remains possible later.
- Scale-out is vertical/horizontal at the process level, not per capability.
