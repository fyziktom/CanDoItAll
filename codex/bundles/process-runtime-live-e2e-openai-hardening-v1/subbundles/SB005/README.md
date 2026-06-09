# SB005 — Startup smoke hardening

## Status
Completed.

## Objective
Run/repair app startup integration test with current composition, process services, template catalog, dispatch service.

## Covered Inputs
- User wants real-code verification, not bundle-report-only confirmation.
- User wants process runtime to work from UI/project structure/API/scheduler/workflow-origin paths.
- User wants stable generic Process Core with domain drivers.

## Prerequisites
- Complete all earlier subbundles in dependency order.
- Re-read current branch source before editing.
- Do not proceed past a critical gate until proof is captured.

## Exact Source References
- `repo://src/CanDoItAll.Modules.Processes`
- `repo://src/CanDoItAll.Modules.Processes/README.md`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch`
- `repo://src/CanDoItAll.Modules.Processes/Runtime`
- `repo://src/CanDoItAll.Modules.SchedulerPlanner/SchedulerPlannerService.cs`
- `repo://src/CanDoItAll.Web`
- `repo://src/CanDoItAll.Composition`
- `repo://tests/CanDoItAll.Tests.Integration`
- `repo://tests/CanDoItAll.Tests.Unit`
- `repo://tests/CanDoItAll.Tests.Playwright`

## Scope
- Implement only this coherent slice. Keep changes source-backed and minimal enough to review, but do not fake completion with table-only proof.

## Dependency Impact
- Downstream phases depend on this subbundle's behavior and proof if it touches startup, launch, run lifecycle, dispatch, UI proof, live OpenAI, or runtime-host decisions.

## Validation Depth
- Standard validation plus source assertions; nearest critical gate will re-run broader proof.

## Implementation Steps
1. Inspect the referenced source files.
2. Make the smallest complete change needed for the objective.
3. Add or update stable tests; do not use concrete bundle folder paths in long-lived tests.
4. Run the subbundle-specific tests.
5. For critical gates, run build/source scans/focused matrices and write proof manifests.

## Scope Exceptions
- No generic driver runtime host, registry, selector, driver DI auto-registration, driver manager command, scheduler driver hook, or workflow driver hook.
- Live OpenAI proof is opt-in only and must be skipped cleanly when configuration is absent.
- No small/medium/mobile UI proof.

## Do Not Do
- Do not add test references to `codex/bundles/<bundle-name>`.
- Do not log API keys or secrets.
- Do not route process starts through driver runtime.
- Do not mutate process state through read-only drivers.
- Do not move runtime orchestration into Process Core.

## Acceptance Checklist
- [x] Source inspected and exact changed files listed.
- [x] Tests updated without transient bundle paths.
- [x] Build/focused tests pass for touched surface.
- [x] No forbidden runtime-host/driver-host drift.
- [x] No UI/media drift unless explicitly scoped.
- [x] Execution report row updated.

## Proof Required
- Source assertion transcript: `bundle://proof/SB005/transcripts/process-module-registration-source-assertions.txt`
- Test transcript: `bundle://proof/SB005/transcripts/process-module-registration-integration-tests.txt`
- Unit test transcript: `bundle://proof/SB005/transcripts/process-runtime-tool-provider-unit-tests.txt`
- Anti-stub scan: `bundle://proof/SB005/transcripts/anti-stub-and-runtime-host-drift-scan.txt`
- No transient bundle-path scan: `bundle://proof/SB005/transcripts/no-transient-bundle-path-scan.txt`
- Registration proof: `bundle://proof/SB005/process-module-registration-proof.md`
- Proof may be consolidated into the next critical gate, but must still name changed files and tests.

## Browser Validation Logging
- N/A unless browser-visible files or flows are changed unexpectedly.

## Progression Gate
- Do not start the next dependent phase unless this subbundle has passed its closure gate and the execution report row is updated.

## Suggested Agent Prompt
Implement SB005 in `process-runtime-live-e2e-openai-hardening-v1`. Keep the process runtime source-backed, avoid transient bundle paths, preserve generic Process Core, and capture artifact-backed proof before moving on.
