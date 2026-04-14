# Performance and scaling note

## Changes made
- `ProcessesService.Persistence.cs` now reuses per-role and per-step lookup buckets for role skills, branch outcomes, dependencies, assignments, and artifact inputs instead of repeatedly scanning the full existing collections inside save loops.
- `ProcessRuntimeProgressionPlanner.cs` now precomputes dependent-step lookups once per transition instead of re-scanning the full step graph for every completion or cascade skip walk.
- `ProcessesService.Support.cs` now precomputes role and branch-outcome lookup sets during publish validation instead of re-scanning steps and dependencies for each branch outcome.
- `ProcessOutbox.cs` now uses one shared process-workspace route builder instead of duplicating route string assembly for definitions and runs.

## Remaining scale assumptions
- `ProcessesService.RuntimeReadQuery.cs:GetAnalyticsAsync` still materializes filtered run ids, step metrics, and conformance flags into memory before composing the final summary.
- That is acceptable for the current scoped dashboard use case where analytics stays bounded to one process definition or one project and expected history remains in the low-thousands range, not unbounded global reporting.
- If analytics becomes cross-project or high-volume, the next safe move is database-side grouped aggregation, not a wider in-memory rewrite of the Process module.

## Why this is still safe
- The changes above reduce obvious repeated scans without moving correctness logic across boundaries or widening mutable shared state.
- Fresh build, integration, and component proof still pass after the cleanup.
