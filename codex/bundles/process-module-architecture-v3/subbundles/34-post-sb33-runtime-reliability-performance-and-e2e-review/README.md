# SB34 Post-SB33 Runtime Reliability, Performance, And E2E Review

## Status

- Completed
- Completed date: 2026-06-20

## Objective

Review the active Process implementation after SB33 and the most recent commits, then apply the smallest safe architecture/performance hardening changes that reduce stuck-run risk, preserve generic runtime/dispatcher boundaries, and keep the TetrisGame e2e process working after the user-cleared project/output state.

## Covered Inputs

- User follow-up captured at `bundle://inputs/post-sb33-architecture-performance-review-20260620.md`.
- v3 architecture analysis and performance guardrails: `bundle://analysis/02-runtime-dispatcher-insufficiency.md`, `bundle://analysis/07-dotnet-performance-antipattern-review.md`, `bundle://architecture/19-dotnet-performance-guardrails.md`.
- SB30 performance hardening proof: `bundle://proof/SB30-performance-hot-path-hardening/manifest.md`.
- SB31-SB33 repair proof and progression notes.

## Prerequisites

- SB30 through SB33 are completed or repaired enough for prepared-stage validation.
- Active source builds before broad e2e validation.
- CodeAnalytics or direct source scans can inspect Process runtime/application/module code.
- The user-cleared TetrisGame project/output state can be used for a fresh e2e run.

## Exact Source References

- `repo://src/CanDoItAll.Modules.Processes/Services/ProcessRuntimeDispatchQueueServices.cs`
- `repo://src/CanDoItAll.Modules.Processes/Services/ProcessRuntimeDispatchQueue.cs`
- `repo://src/CanDoItAll.Modules.Processes/Services/ProcessRuntimeDispatchQueueOptions.cs`
- `repo://src/CanDoItAll.Modules.Processes/Services/ProcessRuntimeIntegrationServices.cs`
- `repo://tests/CanDoItAll.Tests.Unit/ProcessRuntimeDispatchQueueTests.cs`
- `repo://codex/bundles/process-module-architecture-v3/subbundles/32-live-processes-staffing-ui-and-active-agent-repair/README.md`
- `repo://codex/bundles/process-module-architecture-v3/subbundles/33-provider-runtime-node-and-agent-chat-load-repair/README.md`

## Deliverables

- Prepared-stage bundle metadata repair for SB32/SB33.
- Bounded immediate/recovery dispatch queues with explicit capacity options.
- Cancellation-safe dispatch queue dedupe cleanup so a canceled enqueue cannot strand a run.
- Process adapter generated regex methods instead of compiled regex fields.
- Queue/options composition split out of the worker file.
- Focused regression tests and performance/static scan counts.
- Fresh TetrisGame process e2e validation on the updated implementation.
- Artifact-backed proof under `bundle://proof/SB34-post-sb33-runtime-reliability-performance-and-e2e-review/`.

## Implementation Steps

1. Run the bundle prepared-stage validator and repair stale bundle metadata before relying on the bundle gate.
2. Review active source with CodeAnalytics and direct scans for genericity leaks, stuck-run risks, performance guardrail violations, and oversized responsibility clusters.
3. Replace unbounded Process dispatch channels with bounded channels and configurable capacities.
4. Fix queue dedupe cleanup when enqueue write is canceled or fails.
5. Split queue and queue options out of the dispatch worker source file.
6. Convert remaining Process adapter compiled regexes to `[GeneratedRegex]`.
7. Add focused regression tests for queue dedupe/backpressure behavior.
8. Run focused unit tests, module/build validation, and static performance scans.
9. Restart/run the current app build and execute the TetrisGame process e2e from the cleared state.
10. Record proof, hashes, execution-report rows, and final closure notes.

## Dependency Impact

- SB34 changes Process module runtime queue infrastructure and AgentFramework Process adapter parsing only.
- Runtime/dispatcher genericity is preserved: no TetrisGame, project-specific, or domain-specific process rule is added to generic Process contracts.
- Queue capacity options are module-host configuration, not Process core semantics.
- Existing dispatch worker orchestration and recovery queries continue to use the same public queue contract.

## Validation Depth

- Focused unit tests for dispatch queue, dispatch application service, Process integration adapter, and Process metadata behavior.
- Process module or solution build after source changes.
- Static scans for sync-over-async, unbounded Process dispatch channels, compiled/per-call regex in touched Process scope, per-call HTTP clients, and Process genericity leaks.
- CodeAnalytics snapshot health check for scoped architecture inventory.
- Fresh TetrisGame e2e process run against the updated app.

## Do Not Do

- Do not rewrite the Process dispatcher or runtime engine broadly in this hardening pass.
- Do not remove the explicit strategy isolation timeout behavior that prevents a misbehaving strategy from blocking the dispatcher forever.
- Do not add TetrisGame or other scenario-specific rules to generic Process runtime, dispatcher, builder, manager, projections, or driver contracts.
- Do not hide queue saturation or canceled enqueue behavior behind silent fallback.
- Do not turn broad authoring/UI code into speculative micro-optimization work without measured need.

## Acceptance Checklist

- [x] SB32/SB33 bundle metadata is repaired enough for prepared-stage validation.
- [x] Dispatch queue channels are bounded and configurable.
- [x] Canceled queue writes remove their run-id dedupe marker.
- [x] Pending run-id dedupe remains intact until dequeue.
- [x] Process adapter regexes use `[GeneratedRegex]`.
- [x] Queue/options source is separated from worker orchestration.
- [x] Focused tests and builds pass.
- [x] Static performance/genericity scans are recorded.
- [x] TetrisGame process e2e passes after the hardening changes.
- [x] Proof manifest and semantic invariants are recorded.

## Proof Required

- `proof/SB34-post-sb33-runtime-reliability-performance-and-e2e-review/manifest.md`
- `proof/SB34-post-sb33-runtime-reliability-performance-and-e2e-review/semantic-invariants.md`
- `proof/SB34-post-sb33-runtime-reliability-performance-and-e2e-review/changed-file-hashes.txt`
- Focused test/build/static scan transcripts under `proof/SB34-post-sb33-runtime-reliability-performance-and-e2e-review/transcripts/`.
- E2E process proof under `proof/SB34-post-sb33-runtime-reliability-performance-and-e2e-review/e2e/`.

## Browser Validation Logging

- Browser proof is required only if the TetrisGame e2e validation uses the UI or validates UI-visible runtime/live-process behavior.
- API/host proof is acceptable for backend-only queue and adapter hardening, but final e2e proof must include enough run state, artifacts, and cleanup evidence to prove the process still works after the source changes.

## Progression Gate

- SB34 closes only when the queue stuck-run negative case is covered by a failing-first-capable regression test, static scans show no new Process hot-path guardrail violation except documented strategy-isolation `Task.Run`, and the fresh TetrisGame e2e run succeeds on the updated app build.

## Suggested Agent Prompt

Execute SB34 from `codex/bundles/process-module-architecture-v3/subbundles/34-post-sb33-runtime-reliability-performance-and-e2e-review`. Repair SB32/SB33 bundle metadata, harden Process dispatch queue backpressure/dedupe behavior, convert Process adapter regexes to generated regex methods, keep runtime/dispatcher generic, validate with focused tests/builds/static scans, then run the fresh TetrisGame process e2e and record artifact-backed proof.
