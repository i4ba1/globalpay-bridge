# ADR-003: Transactional outbox and inbox

- Status: Accepted
- Date: 2026-09-22
- Source: GBP-ARC-001 §8

## Context

State changes must be atomic with the messages that announce them, without distributed transactions.
External events arrive at least once and may be duplicated, delayed, or reordered.

## Decision

Write an `OutboxMessage` in the same transaction as the aggregate change. A dispatcher leases pending
messages (`usp_LeaseOutboxMessages`, `UPDLOCK`/`READPAST`) and publishes them with bounded exponential
backoff. For inbound webhooks, insert into `ProviderEventInbox` under a unique constraint on
`(ProviderCode, ProviderEventId)` **before** applying the state transition.

## Consequences

- At-least-once delivery with idempotent consumers; no distributed transactions.
- Handlers must be idempotent; replay is safe (FR-X-05).
- Operational visibility is required for poison messages: attempt limits, quarantine state, alerts.
