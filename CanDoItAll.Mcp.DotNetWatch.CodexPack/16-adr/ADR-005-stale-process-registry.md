# ADR-005 — Persist a stale managed process registry

## Status
Accepted

## Context
The server or client may terminate unexpectedly while managed child processes continue running.

## Decision
Persist minimal ownership metadata for managed processes in a registry file, and clean up stale records/processes at startup and via a manual cleanup tool.

## Rationale
- prevents orphaned app/watch processes
- reduces locked binaries and occupied ports
- gives the server a recovery path after its own crash

## Consequences
Positive:
- better reliability
- safer restart behavior
- auditability of cleanup actions

Negative:
- must verify ownership carefully before killing anything
- stale registry schema becomes part of runtime compatibility

## Follow-up
Keep the registry minimal and workspace-scoped.
