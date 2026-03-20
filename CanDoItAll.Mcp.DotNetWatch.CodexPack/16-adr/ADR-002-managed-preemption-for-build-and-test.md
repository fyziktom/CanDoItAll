# ADR-002 — Build and test use managed preemption when the app is running

## Status
Accepted

## Context
The CanDoItAll app may already be running under a managed watch or run session when a build or test request arrives. That creates a high risk of binary locks, stale outputs, or inconsistent workflow if the client tries to improvise stop/start behavior.

## Decision
Build and test operations must go through a policy layer:
- `StopAndResume`
- `StopOnly`
- `Fail`
- `ContinueIfSafe`

Default policy for CanDoItAll: `StopAndResume`.

## Rationale
- avoids lock-related flakiness
- keeps lifecycle decisions centralized
- prevents the client from inventing ad hoc behavior
- preserves a deterministic workflow for UI iteration

## Consequences
Positive:
- more reliable build/test execution
- clear state transitions
- better diagnostics

Negative:
- more implementation complexity
- longer end-to-end operation time in some cases

## Follow-up
`ContinueIfSafe` may remain conservative in MVP.
