# ADR-009: Keycloak locally, Microsoft Entra ID in Azure

- Status: Accepted
- Date: 2026-09-22
- Source: Delivery decision (Phase 0)

## Context

The platform requires OIDC bearer authentication with tenant and role claims. Using a cloud identity
tenant for local development adds a hard external dependency and slows the inner loop; using a
hand-rolled token issuer would not exercise real OIDC validation.

## Decision

Run Keycloak in the Docker Compose profile with an imported realm (`infrastructure/keycloak`) that
issues `tenant_id` and `role` claims and an `aud` of `globalpay-api`. In Azure, use Microsoft Entra ID
with equivalent app roles and a claims-mapping policy producing the same claim names. The API depends
only on JWT bearer validation with configurable authority/audience; it is unaware of the issuer
product.

## Consequences

- The inner loop is fully self-contained and offline-capable.
- Claim shape parity is the risk: it is mitigated by keeping claim names (`tenant_id`, `role`,
  `preferred_username`) as the single contract and validating with the same code path in both
  environments.
- `RequireHttpsMetadata` is disabled only in the Development environment.
