# SB09 - Typed Blocked/Failed Escalation Lifecycle

## Status


- Completed

## Objective

Replace fragile free-text blocked reasons with typed block codes and recovery options.

## Covered Inputs

- RQ09
- VF05
- N001
- N004

## Prerequisites

- SB01-SB08 closure gates passed.
- Runtime invariant audit records severe violations that can drive typed recovery.

## Exact Source References

- repo://src/CanDoItAll.Modules.Processes/Persistence/Entities/ProcessRuntimeModels.cs
- repo://src/CanDoItAll.Modules.Processes/Persistence/Configurations/ProcessRuntimeEntityConfigurations.cs
- repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessRuntimeProgressionPlanner.cs
- repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessStepRunTransitions.cs
- repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessesService.Runtime.StepTransitions.cs
- repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Dispatch.cs
- repo://tests/CanDoItAll.Tests.Integration/ProcessesServiceIntegrationTests.cs
- repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs

## Scope

- Typed block reason codes and recovery-option metadata for step runs.
- Typed codes for missing upstream artifact, policy denial, unavailable tool, missing credentials, validation failure, no-progress, and invariant violation.
- Reactivation and recovery paths that do not depend on BlockedReason substring matching.

## Dependency Impact

- Critical subbundle.
- SB10 validates blocked/failed semantics across generic scenarios.

## Validation Depth

- Failing-first or red-team test transcript.
- Focused unit/integration tests named in the proof manifest.
- Source assertions against production code paths.
- Anti-stub audit.
- Changed-file SHA-256 hashes.
- Full build and PostgreSQL-only audit before final closure.

## Implementation Steps

- Add failing tests for materialization reactivation by changed/free-text blocked reason.
- Add typed block code and recovery options to step run state and transitions.
- Update reactivation, policy denial, and no-progress paths to set typed state.
- Update proof artifacts after focused lifecycle tests pass.

## Do Not Do

- Do not add SQLite support or SQLite-specific runtime/migration paths.
- Do not confuse workflow executor state with process-owned lifecycle, finalization, or governance.
- Do not replace runtime enforcement with prompt-only wording.
- Do not hard-code software-delivery-only behavior into generic process services.
- Do not mark this subbundle complete without artifact-backed proof under proof/SB09/.

## Acceptance Checklist

- [ ] Materialization block reopens by typed code.
- [ ] Policy-denied external path becomes actionable escalation.
- [ ] Repeated no-progress becomes typed recovery/stop condition.
- [ ] Old shallow behavior fails or is red-team documented.
- [ ] New production behavior passes through runtime code, not prompt text.
- [ ] Proof manifest and semantic invariants cite existing artifacts.
- [ ] No SQLite runtime reintroduction.

## Proof Required

Update:

- proof/SB09/manifest.md
- proof/SB09/semantic-invariants.md
- proof/SB09/transcripts/failing-first.txt
- proof/SB09/transcripts/passing.txt
- proof/SB09/transcripts/source-assertions.txt
- proof/SB09/transcripts/anti-stub-audit.txt
- proof/SB09/transcripts/changed-file-hashes.txt

## Browser Validation Logging

- Required if blocked/failed state UI changes.
- Add a row in reviews/01-execution-report.md while validation is fresh.

## Progression Gate

- Entry gate must confirm prerequisites and exact source references still match the repo.
- Closure gate must confirm tests, source assertions, anti-stub audit, changed-file hashes, and proof manifest are complete.
- Downstream subbundles must re-check this gate if later observations weaken the proof.

## Suggested Agent Prompt

Implement SB09 from codex/bundles/processes-hardening-followup-runtime-governance-v5. Preserve generic process semantics, keep Processes above Workflows, and capture artifact-backed proof before moving on.
