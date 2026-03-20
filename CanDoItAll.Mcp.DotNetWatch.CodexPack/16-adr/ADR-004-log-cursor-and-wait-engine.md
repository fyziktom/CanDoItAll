# ADR-004 — Use log cursors and a dedicated wait engine

## Status
Accepted

## Context
Client-side sleeps are unreliable for app startup, watch restarts, and long-running build/test operations.

## Decision
Implement:
- monotonic log cursors
- a ring log buffer
- an explicit wait engine for app and operation conditions

## Rationale
- deterministic waiting
- incremental log retrieval
- better failure evidence
- simpler client behavior

## Consequences
Positive:
- less flaky automation
- easier debugging
- better Codex ergonomics

Negative:
- more internal state to manage

## Follow-up
Persisted log storage is recommended for troubleshooting but not for live re-attachment.
