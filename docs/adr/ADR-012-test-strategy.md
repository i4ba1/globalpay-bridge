# ADR-012: Test strategy — in-memory provider for rules, Testcontainers for SQL behaviour

- Status: Accepted
- Date: 2026-09-22
- Source: Delivery decision (Phase 0), GBP-ARC-001 §7

## Context

Tests must be fast and deterministic in CI, but some behaviour (filtered indexes, stored procedures,
`rowversion`, window functions) only exists in SQL Server. Running containers for every test is slow
and unavailable in some environments.

## Decision

Split verification:

- **Unit tests** cover domain and application rules with no infrastructure (xUnit, Moq).
- **Architecture tests** enforce layer and module boundaries (NetArchTest).
- **Integration tests** use the EF Core in-memory provider to verify provider-agnostic behaviour such
  as global query filters and tenant isolation, and use Testcontainers (SQL Server, Redis, Azurite) for
  behaviour that requires the real engine.

The tenant-isolation tests deliberately construct two `DbContext` instances sharing one options object
but with different tenant contexts, proving that the global query filter is bound to the context
instance rather than to a captured service (see `GlobalPayDbContext`).

## Consequences

- The default suite is fast and runs anywhere; container-backed tests are additive.
- Providers can diverge; any behaviour that depends on SQL Server must be covered by a
  container-backed test, not by the in-memory provider.
- Coverage gates (NFR-08) are measured on the default suite.
