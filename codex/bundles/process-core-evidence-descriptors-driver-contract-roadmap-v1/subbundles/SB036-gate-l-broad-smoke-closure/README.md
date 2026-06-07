# SB036 — Gate L: broad smoke closure

## Status
Prepared.

## Objective
Source scans, anti-stub, UI/media drift scan, driver token scan.

## Covered Inputs
- `inputs/raw-user-request.md`
- `analysis/01-current-review-summary.md`
- `architecture/02-stable-core-driver-roadmap.md`

## Prerequisites
- Previous subbundle in phase completed.
- If this is a gate, all previous subbundles in the phase must have proof artifacts.

## Exact Source References
- `/src/CanDoItAll.Processes.Core`
- `/src/CanDoItAll.Modules.Processes/Automation/Dispatch`
- `/tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs`
- `/tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs`
- `/codex/bundles/process-core-stabilization-diagnostics-driver-roadmap-v1`
- `/codex/bundles/process-core-pure-rules-expansion-subprocess-artifact-driver-readiness-v1`

## Scope
- Implement only the named slice.
- Preserve current behavior and adapter boundaries.
- Keep Core pure and deterministic.

## Dependency Impact
Critical foundation for downstream phases. If this gate fails, stop and repair earlier subbundles.

## Validation Depth
Critical gate: build/test/source-scan proof required.

## Implementation Steps
1. Inspect current source and previous proof.
2. Make the smallest complete source/doc/test movement for this slice.
3. Update or add focused architecture tests.
4. Run targeted proof.
5. Record transcript and update execution report.

## Scope Exceptions
- Do not create production process-driver APIs.
- Do not move side-effectful process behavior into Core.
- Do not touch UI/media unless explicitly required by failed tests; unexpected UI changes should fail the bundle.

## Do Not Do
- No EF, workspace/storage/filesystem, AgentFramework execution, claim lifecycle, transition execution or finalizer application inside Core.
- No production driver registry/DI/runtime selector/manager command.
- No broad dispatcher file movement.

## Acceptance Checklist
- [ ] Source change is limited to this subbundle objective.
- [ ] Core remains dependency-clean.
- [ ] Existing behavior is preserved by focused proof.
- [ ] Execution report row is updated.
- [ ] No TODO/stub/NotImplemented markers added.

## Proof Required
- Build/test transcript appropriate to this slice.
- Source scan for forbidden Core/driver/UI/stub tokens.
- If gate: phase manifest and semantic invariants.

## Browser Validation Logging
N/A runtime/Core/service refactor. If UI files change unexpectedly, fail the subbundle and explain why.

## Progression Gate
Must pass before downstream phase may start.

## Suggested Agent Prompt
Implement SB036 from `process-core-evidence-descriptors-driver-contract-roadmap-v1`. Keep Core pure, preserve behavior, and record proof before proceeding.
