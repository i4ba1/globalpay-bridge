# ADR-008: Bicep infrastructure as code

- Status: Accepted
- Date: 2026-09-22
- Source: GBP-ARC-001 §8

## Context

Environments must be reproducible, reviewable, and consistent with the local profile. Manual portal
changes are unreviewable and drift.

## Decision

Define all Azure resources as Bicep modules under `infrastructure/bicep`, parameterised per
environment, and deploy through GitHub Actions using OIDC federated credentials with protected
environments and approvals. Local development uses Docker Compose with the same configuration keys
and health contracts as Azure.

## Consequences

- Environment creation is repeatable and diff-reviewable in pull requests.
- Configuration keys must stay in parity between the Docker profile and Azure; drift is caught by
  smoke tests after deployment.
- Bicep delivery lands in release R2; Phase 0 ships the folder contract and local profile only.
