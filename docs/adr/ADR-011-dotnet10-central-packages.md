# ADR-011: .NET 10 LTS with central package management

- Status: Accepted
- Date: 2026-09-22
- Source: Delivery decision (Phase 0)

## Context

The solution spans ten projects that must share consistent dependency versions and compiler settings.
Version drift across projects is a common source of build and runtime inconsistency.

## Decision

Target `net10.0` (LTS), pinned by `global.json` (`rollForward: latestFeature`). Use Central Package
Management (`Directory.Packages.props`) so versions are declared once, and `Directory.Build.props` for
shared compiler and analyzer settings: nullable enabled, warnings as errors, latest-recommended
analysis, deterministic builds, invariant globalization. Code style is enforced through
`.editorconfig` with `EnforceCodeStyleInBuild`.

## Consequences

- A version bump is a single-line change; per-project versions are impossible.
- Warnings fail the build, so unused usings, style violations, and analyzer findings are fixed rather
  than accumulated. A small, explicit `NoWarn` list documents deliberate suppressions.
- `InvariantGlobalization` means no culture-specific formatting; money and dates format invariantly by
  design.
