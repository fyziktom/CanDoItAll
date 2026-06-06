# process-dispatch-main-loop-claim-lifecycle-boundary-v1

Status: Prepared for Codex implementation.

## Mission

Continue the `maf-processes-refactor` branch with a larger, dependency-aware runtime refactor that isolates the remaining main dispatch-loop and durable claim lifecycle from `ProcessRunAutomationDispatchService.Dispatch.cs`.

This bundle is intentionally **not** a Process Core split. It is a module-local boundary step that makes a later Core extraction safer.

## Primary Goals

1. Keep every existing process automation behavior intact.
2. Extract durable dispatch claim persistence and heartbeat lease behavior into explicit module-local boundaries.
3. Extract dispatch route execution stages into a route pipeline/facade that preserves the current order exactly.
4. Extract exception/failure closure behavior into explicit helpers so `DispatchAsync` becomes mostly orchestration.
5. Reduce `ProcessRunAutomationDispatchService.Dispatch.cs` materially without hiding side effects in fake-pure helpers.
6. Keep future driver preparation documentation-only.
7. Prove all changes with focused tests, source scans, build proof and red-team review.

## Hard Non-Goals

- Do not create `CanDoItAll.Processes.Core`.
- Do not move EF entities, migrations, Razor pages, UI components, or public module APIs.
- Do not create production driver APIs such as `IProcessDriverPack`, `IProcessDriverRegistry`, `ProcessDriverRegistry`, `IProcessHelperDriver`, or driver packages.
- Do not change process route order, claim lease semantics, heartbeat renewal semantics, failure transition behavior, retry semantics, subprocess behavior, workflow behavior, or artifact projection behavior.
- Do not add small/medium/mobile/browser screenshots. This is a runtime/service refactor; browser validation is N/A unless UI files are unexpectedly touched, which should fail the gate.

## Critical Reminder

This bundle exists because the prior projection model/rule decoupling closed successfully, but `Dispatch.cs` still owns too much. The next boundary should remove hidden coupling from the main dispatch loop before any Process Core extraction.

## Required Closure

Codex must close every subbundle row individually. Do **not** collapse SB01-SB96 into one row in the execution report. Every critical gate must have a manifest, semantic invariants, source scan transcript and test/build proof.
