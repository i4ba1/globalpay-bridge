# ADR-006: Currency-specific decimal handling

- Status: Accepted
- Date: 2026-09-22
- Source: GBP-ARC-001 §8, GBP-DBD-001 §1

## Context

Binary floating point cannot represent decimal money exactly. Mixing currencies silently produces
meaningless totals, and inconsistent rounding produces reconciliation mismatches.

## Decision

Model money with a `Money` value object carrying a `decimal(19,4)` amount and a `Currency` value
object validated against an ISO 4217 allow-list. Rounding uses banker's rounding
(`MidpointRounding.ToEven`) at four decimal places. Arithmetic across different currencies throws a
`DomainException` with code `money.currency_mismatch`. Currency minor units are recorded for
display/presentation rules.

## Consequences

- Totals are exact and reproducible; determinism supports FR-PAY-02.
- Every monetary operation must carry its currency; cross-currency conversion is explicit and out of
  scope for MVP.
- Persisted precision is fixed at `decimal(19,4)` per the database design.
