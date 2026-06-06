# SB38 - Gate G - provider-native parity

## Status

- Status: Completed

## Objective

Focused tests and scans prove provider-native browser top-level coordinator split.

## Covered Inputs

- Original request: continue smaller dispatcher isolation, do not rush Process Core, preserve original functionality, plan more safe phases, and avoid UI/mobile proof.
- Branch review: the current projection coordinator boundary is still nested and needs top-level module-local splitting plus dependency narrowing.

## Prerequisites

- Previous subbundle closure gate must pass and any critical prerequisite proof must remain trusted.

## Exact Source References

- repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ArtifactProjection.cs
- repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ArtifactProjectionCoordinators.cs
- repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessArtifactProjectionPlanner.cs
- repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessArtifactProjectionWriteCoordinator.cs
- repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessArtifactProjectionLineageBuilder.cs
- repo://tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs
- repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs

## Deliverables

- proof/SB38/manifest.md

## Dependency Impact

- Phase G critical foundation. Downstream subbundles must stop if this gate fails or proof is weakened.

## Validation Depth

- Critical gate: build or focused test transcript, source assertion transcript, anti-stub audit, no-core/no-driver scan, no-UI/prohibited-viewport scan, semantic adequacy evidence, and downstream dependency decision.

## Implementation Steps

1. Re-read the root README, phase plan, traceability rows, raw inputs, and this subbundle before editing code.
2. Make only the movement, proof, or documentation change owned by this subbundle.
3. Preserve projection source-family order, candidate state semantics, duplicate handling, side effects, and error/log behavior.
4. Add or update focused tests only when this slice changes behavior risk or architecture assertions.
5. Capture command transcripts under bundle://proof/SB38/transcripts/ when the subbundle produces proof.
6. Update bundle://reviews/01-execution-report.md with gate and browser analytics rows while proof is fresh.

## Scope Exceptions

- No Process Core in this subbundle.
- No production process-driver API in this subbundle.
- Browser validation remains N/A unless an out-of-scope UI edit is detected and reverted.

## Do Not Do

- Do not create CanDoItAll.Processes.Core.
- Do not add IProcessDriverPack, IProcessDriverRegistry, ProcessDriverRegistry, or driver packages.
- Do not touch UI/Razor/CSS/JS/TS files.
- Do not create small/medium/mobile/phone/tablet proof artifacts.
- Do not change projection source-family order.
- Do not remove behavior without focused tests.

## Acceptance Checklist

- [x] Behavior-preserving refactor or proof-only step is complete.
- [x] Exact source-family order is preserved if projection orchestration is touched.
- [x] No broad hidden dispatcher dependency is introduced.
- [x] Side effects remain explicit and named.
- [x] Focused tests or source scans prove the owned change.
- [x] Execution report is updated.
- [x] No UI/prohibited viewport proof paths were created.

## Proof Required

- Build or focused test transcript as applicable.
- Source assertion transcript tied to the owned behavior or boundary.
- Anti-stub scan for changed projection production files.
- No-core/no-driver scan.
- No-UI/no-prohibited-viewport scan.
- Execution report gate row and browser analytics row.
- Artifact-backed manifest at bundle://proof/SB38/manifest.md. - Semantic invariant contract at bundle://proof/SB38/semantic-invariants.md or .json. - Semantic Adequacy Gate: shallow-pass trap, adversarial negative proof, semantic positive proof, anti-stub audit, and raw-note literal closure.

## Browser Validation Logging

- N/A expected. Runtime/service-only refactor. If UI files change, revert the UI changes instead of adding browser or small/medium/mobile proof.

## Progression Gate

- Do not proceed downstream until bundle://proof/SB38/manifest.md, semantic invariants, required transcripts, execution-report rows, and downstream dependency decision are complete.

## Suggested Agent Prompt

Implement SB38 only. Do not jump ahead. Preserve behavior and update proof before moving on.
