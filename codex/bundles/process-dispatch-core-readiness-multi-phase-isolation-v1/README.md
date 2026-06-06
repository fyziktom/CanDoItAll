# process-dispatch-core-readiness-multi-phase-isolation-v1

## Status
Prepared for implementation.

## Objective
Finish the remaining **module-local process dispatch isolation steps** that should land before any `CanDoItAll.Processes.Core` extraction is attempted. This bundle intentionally does **not** create Process Core and does **not** introduce production process-driver APIs. It focuses on burning down remaining dispatcher-centered adapters, reducing huge partials, and making future extraction seams obvious and testable.

## Why this bundle is different from the last few bundles
The previous bundles often sliced one seam into many tiny subbundles. This bundle uses **larger, phase-oriented subbundles**. Each subbundle owns a coherent chunk of isolation work and must produce meaningful source movement, tests, and proof. Codex must not complete the bundle by making only trivial file renames or wrapper-only moves.

## Current branch baseline
Branch under review: `maf-processes-refactor`.

Latest observed state:
- `process-dispatch-route-service-model-decoupling-boundary-v1` reports `Completed`.
- Route-facing dispatcher nested model references were removed from route-facing files, with remaining bridge limited to route model adapters.
- Route services are still largely adapter services forwarding to `ProcessRunAutomationDispatchService`.
- No Process Core or production driver API has been introduced.
- Browser/UI/mobile proof remains N/A for runtime/service-only refactors.

## Hard constraints
- Do **not** create `CanDoItAll.Processes.Core` in this bundle.
- Do **not** introduce `IProcessDriverPack`, `IProcessDriverRegistry`, driver packages, or production driver APIs.
- Do **not** remove, simplify, weaken, or bypass any existing runtime behavior.
- Do **not** change UI, Razor, CSS, JS, TS, screenshots, or viewport proof artifacts.
- Do **not** test small/medium/mobile screen. If UI is unexpectedly touched, stop and treat it as scope drift.
- Do **not** collapse the execution report into one row per phase; each subbundle must have its own row, but subbundles are intentionally larger than before.

## High-level phases
1. Baseline and proof hardening.
2. Route adapter burn-down.
3. Candidate hydration and assignment boundary.
4. Pre-execution guard and transition service boundary.
5. Subprocess runtime and subprocess artifact projection boundary.
6. Finalizer, transition, and exception closure boundary.
7. Dispatcher facade slimming and static-wrapper burn-down.
8. Core-readiness and driver-readiness closure.

## Required final proof
- `dotnet build CanDoItAll.slnx --no-restore`.
- Focused unit tests for route/claim/candidate/subprocess/finalizer boundary behavior.
- Focused integration tests covering process dispatch happy path, retry/recovery path, subprocess path, materialization path, artifact projection path, and workflow path where test infrastructure exists.
- Source scans proving no Process Core, no production driver API, no UI/mobile proof drift, no stubs/TODO placeholders, no broad dispatcher re-coupling.
- Line-count/source-size proof for `Dispatch.cs`, `RouteExecution.cs`, route services, subprocess-related files, finalizer transition files, and candidate hydration files.
