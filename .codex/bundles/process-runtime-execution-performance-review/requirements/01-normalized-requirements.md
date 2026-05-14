# Normalized Requirements

## Requirements

- R001: Run a standard .NET performance anti-pattern scan over `CanDoItAll.Modules.Processes` and record exact hit counts.
- R002: Prioritize only findings that sit on likely hot paths for process start, transition, dispatch, artifact projection, or live observation.
- R003: Preserve generic process semantics; no .NET-app-specific process behavior may move into runtime core.
- R004: Preserve existing process lifecycle behavior, artifact validation, subprocess behavior, manager directives, automation dispatch, and public APIs.
- R005: Reduce repeated per-step runtime-start scans of role requirements, artifact expectations, and effective assignments.
- R006: Avoid introducing hidden fallback behavior; errors must remain explicit and predictable.
- R007: Validate with targeted process tests and full build-level proof appropriate to the change.
- R008: Validate independent simple .NET app builds outside the Processes module to cover the user's app-building smoke request.

## Acceptance Criteria

- The performance scan checklist contains exact counts for each executed recipe.
- `StartRunAsync` no longer filters all step role requirements and artifact expectations once per step.
- Effective assignment resolution is computed once per step and reused by current-executor and capability-gap selection.
- Existing targeted process integration tests pass.
- At least one mock-agent process test is run or an explicit validation gap is recorded.
- At least two independent simple .NET app build smoke cases pass or blockers are recorded.
