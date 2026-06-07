# Structured Input

## Objectives
- Preserve all current process automation behavior while reducing coupling that blocks a later Process Core discussion.
- Burn down route source-payload usage, finalizer dispatcher aliases, hydration/service entanglement, subprocess projection coupling, direct-agent DTO bloat, artifact projection drift, and static wrapper clutter.
- Produce driver-readiness documentation without adding production driver APIs.
- End with an evidence-backed decision on whether the next bundle may start a narrow Process Core project.

## Hard Constraints
- Do not create a Process Core project.
- Do not introduce `IProcessDriverPack`, `IProcessDriverRegistry`, `ProcessDriverRegistry`, or equivalent production driver APIs.
- Do not change UI, Razor, CSS, JS, TS, or media files unless source inspection proves it is required; expected browser proof is N/A.
- Preserve behavior and test coverage for automation dispatch, finalization, hydration, materialization, subprocess lifecycle, direct-agent execution, projection, validation, and retry/provider flows.

## Validation Expectations
- Build passes.
- Full unit tests pass.
- Focused process dispatch/integration tests pass.
- Source scans prove no Core project, no driver API, no UI/media drift, no stubs, and no route/projection/finalizer source-payload regressions.
- Critical gates include artifact-backed proof manifests and semantic invariants before downstream phases proceed.
