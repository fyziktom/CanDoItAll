# ADR-003 — Do not use `dotnet watch test` in MVP

## Status
Accepted

## Context
The server needs reliable test execution under a controlled orchestration model. `dotnet watch test` introduces avoidable complexity and instability for this use case.

## Decision
In MVP, all test execution uses:
- `dotnet test`

The server must not use:
- `dotnet watch test`

## Rationale
- simpler lifecycle
- more predictable output
- easier timeout handling
- better fit for operation-based polling and resume policies

## Consequences
Positive:
- simpler and more stable design
- cleaner test operation model

Negative:
- no test-watch loop in MVP

## Follow-up
Re-evaluate only if future evidence shows a strong benefit and stable platform behavior.
