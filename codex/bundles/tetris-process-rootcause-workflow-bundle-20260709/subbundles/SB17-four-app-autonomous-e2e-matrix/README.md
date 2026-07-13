# SB17 Four-App Autonomous E2E Matrix

## Status

- `Planned`

## Objective

Prove the repaired process autonomously delivers four distinct Blazor WebAssembly applications without product-caused escalation or sample-specific runtime/dispatcher behavior.

## Covered Inputs

- `bundle://inputs/04-persistent-repair-and-four-app-e2e-request.md`
- SB15 and SB16 implementation proof.

## Prerequisites

- Focused tests, full build, architecture review gate, and fresh CodeAnalytics dependency/cycle audit pass.
- Instance 5032 is rebuilt/restarted and exposes current process and agent templates.
- Provider remains `gpt-5.4-mini`.

## Exact Source References

- `repo://src/App/CanDoItAll.Web/Api/ProcessesApi.cs`
- `repo://src/App/CanDoItAll.Web/Api/AgentsApi.cs`
- `repo://src/App/CanDoItAll.Web/Api/ProjectsApi.cs`
- `repo://Templates/Processes/processes/software-delivery/definition.json`
- `bundle://subbundles/SB16-dotnet-quality-repair-subprocess/README.md`
- `bundle://inputs/04-persistent-repair-and-four-app-e2e-request.md`

## Deliverables

- Clean Tetris and Calculator autonomous reruns.
- Project structures, workflow input artifacts, and clean autonomous runs for:
  - an IndexedDB work-time logger with note suggestions, timezone configuration, history, and statistics;
  - an SVG workspace planner with interactive nodes/connections, pan/zoom, persistence, validation-friendly state, and export/import behavior.
- Selective process/agent analytics for retries, repair branches, bughunt handoffs, and escalations.

## Validation Depth

- Production-path process/API evidence, generated-product build/test/runtime/browser proof, and architecture no-leak audit.

## Dependency Impact

- Project-structure additions are test inputs, not source-code dependencies.
- Any source repair discovered during E2E must remain within SB15 or SB16 boundaries and rerun their focused gates.

## Implementation Steps

1. Run focused/full tests, build, architecture review, and CodeAnalytics dependency/cycle checks.
2. Rebuild/restart 5032 and verify seeded process/agent definitions plus `gpt-5.4-mini` provider settings.
3. Clean Tetris safely, preflight, launch, observe, and reopen the responsible phase for every product-caused escalation until clean completion.
4. Repeat the clean observer-only proof for Calculator.
5. Create the work-time logger project structure and workflow input, then clean/preflight/launch/observe to completion.
6. Create the SVG workspace planner project structure and workflow input, then clean/preflight/launch/observe to completion.
7. Run final source leak scan, CodeAnalytics audit, proof manifest validation, and bundle closure gate.

## Do Not Do

- Do not manually transition, approve, rework, or edit generated product source.
- Do not delete workflow/source input artifacts.
- Do not accept a process run with a known fatal UI, console error, failed test, or blocked repair proof.
- Do not add any of the four app names/features to generic runtime/dispatcher code.

## Acceptance Checklist

- Every run starts from verified clean projections/output and passes launch preflight.
- Every product completes with build/test and meaningful browser interaction proof.
- No product-caused escalation remains.
- Any discovered core/driver/template defect reopens SB15 or SB16, is repaired with tests, and the affected run restarts from scratch.
- Source scans and CodeAnalytics prove no sample-specific generic dependency or cycle.

## Proof Required

- `bundle://proof/SB17/manifest.md`
- `bundle://proof/SB17/semantic-invariants.md`
- Cleanup, preflight, launch, process, agent, product validation, source assertion, CodeAnalytics, and red-team transcripts for all four apps.

## Browser Validation Logging

- For every UI run record route, viewport, primary interaction, before/after state, screenshot path, browser snapshot path, console log path, startup receipt, and cleanup receipt.

## Progression Gate

- Bundle closure requires all four autonomous runs plus final prepared/executed bundle validator and architecture review gate.

## Suggested Agent Prompt

Act only as an observer-manager: clean exact run output safely, preflight and launch each app, inspect process and agent evidence, repair only platform defects, restart affected runs from scratch, and prove four generic Blazor deliveries without manual product edits.
