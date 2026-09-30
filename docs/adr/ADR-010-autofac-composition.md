# ADR-010: Autofac for module composition in the API host

- Status: Accepted
- Date: 2026-09-22
- Source: GBP-ARC-001 §4

## Context

`GBP-ARC-001` states that Autofac composes modules through explicit registration modules. Cross-cutting
concerns (for example replacing the system `ICurrentUser` with the HTTP-backed implementation) need to
override registrations made by inner layers.

## Decision

The API host uses `AutofacServiceProviderFactory` and registers a `GlobalPayApiModule` that owns
host-specific overrides and module wiring. Application and Infrastructure expose their registrations
through plain `IServiceCollection` extension methods (`AddApplication`, `AddInfrastructure`) so they
stay container-agnostic and testable. The Functions host uses the built-in isolated-worker container
with the same extension methods.

## Consequences

- Inner layers do not reference Autofac; the dependency points inward (DIP).
- Hosts differ only in composition, not in behaviour.
- Two composition styles exist by design; both consume the same `IServiceCollection` contracts.
