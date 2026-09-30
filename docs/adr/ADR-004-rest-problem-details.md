# ADR-004: REST contracts with RFC 9457 problem details

- Status: Accepted
- Date: 2026-09-22
- Source: GBP-ARC-001 §8, GBP-API-001 §1/§4

## Context

API consumers need a stable, interoperable contract with predictable failure semantics. Ad-hoc error
shapes force consumers to parse prose and make automated handling fragile.

## Decision

Expose a versioned REST API under `/api/v1` with camelCase JSON and UTC ISO 8601 timestamps. Return
errors as RFC 9457 problem details containing a stable application `code`, a safe `detail`, and a
`correlationId`. Expected failures are modelled as `Result`/`Result<T>` rather than exceptions; an
`IExceptionHandler` maps unhandled and domain exceptions to the same shape.

## Consequences

- Consumers can branch on `code`; support can trace by `correlationId`.
- Handlers avoid exception-driven control flow and stay testable.
- OpenAPI is generated in CI and checked for breaking changes (FR-X-04).
