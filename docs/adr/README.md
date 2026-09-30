# Architecture Decision Records

Each ADR records a decision that is expensive to reverse. ADR-001..008 are mandated by
`GBP-ARC-001` §8; ADR-009..012 record decisions made while delivering the Phase 0 foundation.

| ADR | Decision | Status |
|-----|----------|--------|
| [ADR-001](ADR-001-modular-monolith-first.md) | Modular monolith first | Accepted |
| [ADR-002](ADR-002-ef-core-plus-dapper.md) | EF Core for aggregates, Dapper/stored procedures for reporting | Accepted |
| [ADR-003](ADR-003-outbox-and-inbox.md) | Transactional outbox and inbox | Accepted |
| [ADR-004](ADR-004-rest-problem-details.md) | REST contracts with RFC 9457 problem details | Accepted |
| [ADR-005](ADR-005-azure-managed-identity.md) | Azure managed identity over stored credentials | Accepted |
| [ADR-006](ADR-006-currency-decimal-handling.md) | Currency-specific decimal handling | Accepted |
| [ADR-007](ADR-007-versioned-calculation-snapshot.md) | Versioned calculation snapshots | Accepted |
| [ADR-008](ADR-008-bicep-infrastructure-as-code.md) | Bicep infrastructure as code | Accepted |
| [ADR-009](ADR-009-identity-provider-parity.md) | Keycloak locally, Microsoft Entra ID in Azure | Accepted |
| [ADR-010](ADR-010-autofac-composition.md) | Autofac for module composition in the API host | Accepted |
| [ADR-011](ADR-011-dotnet10-central-packages.md) | .NET 10 LTS with central package management | Accepted |
| [ADR-012](ADR-012-test-strategy.md) | In-memory provider for rules, Testcontainers for SQL behaviour | Accepted |
| [ADR-013](ADR-013-validation-and-pipeline.md) | Boundary validation with endpoint filters, business rules as results | Accepted |
