# ADR-005: Azure managed identity over stored credentials

- Status: Accepted
- Date: 2026-09-22
- Source: GBP-ARC-001 §8

## Context

Long-lived cloud credentials in configuration are a persistent leak risk and a rotation burden.

## Decision

Grant application identities access to Azure SQL, Blob Storage, Key Vault, and Service Bus-style
resources through managed identity. Secrets are read from Key Vault by reference, never stored in the
repository or in application settings. `DefaultAzureCredential` is used so the same code path works
locally (developer credentials or emulator connection strings) and in Azure (managed identity).

## Consequences

- No long-lived secrets in source control; RBAC is the authorization surface.
- Local development uses Azurite/SQL containers with development-only connection strings held outside
  source control where possible.
- Blob SAS generation requires shared-key or user-delegation credentials; where only managed identity
  is available, user-delegation SAS must be used (tracked for R2).
