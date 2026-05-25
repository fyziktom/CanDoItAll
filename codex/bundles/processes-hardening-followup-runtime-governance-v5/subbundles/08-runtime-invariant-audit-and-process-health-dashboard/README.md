# SB08 - Runtime Invariant Audit

## Status


- Completed

## Objective

Add durable post-step invariant auditing and surface violations in process health.

## Covered Inputs

- RQ08
- VF10
- N001
- N004
- N005

## Prerequisites

- SB01-SB07 closure gates passed.
- Contracts, policy, grounding, lineage, mappings, and recovery are stable enough to audit.

## Exact Source References

- repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.StepCompletionFinalizer.cs
- repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessRuntimeViewModels.cs
- repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessesService.RuntimeReadQuery.cs
- repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessRuntimeStateOverviewService.cs
- repo://src/CanDoItAll.Modules.Processes/Components/ProcessWorkspaceRunsOperatorConsoleSection.razor
- repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs
- repo://tests/CanDoItAll.Tests.Integration/ProcessRuntimeReadQueryServiceTests.cs

## Scope

- Durable ProcessRuntimeInvariantViolation records or equivalent journal entries.
- Audit of tool receipts, artifact lineage, changed paths, branch disposition, and operation contract.
- Severe violation block/escalation behavior.
- Run/step health view model exposure.

## Dependency Impact

- Critical subbundle.
- SB09 uses invariant violations as typed blocked/failed reasons; SB10 red-team closure depends on audit visibility.

## Validation Depth

- Failing-first or red-team test transcript.
- Focused unit/integration tests named in the proof manifest.
- Source assertions against production code paths.
- Anti-stub audit.
- Changed-file SHA-256 hashes.
- Full build and PostgreSQL-only audit before final closure.

## Implementation Steps

- Add failing tests where a non-mutating step has a product mutation receipt.
- Implement invariant audit producer after execution/finalization.
- Persist and expose violations through read models and dashboard state.
- Add browser proof if dashboard rendering changes.
- Update proof artifacts after focused audit tests pass.

## Do Not Do

- Do not add SQLite support or SQLite-specific runtime/migration paths.
- Do not confuse workflow executor state with process-owned lifecycle, finalization, or governance.
- Do not replace runtime enforcement with prompt-only wording.
- Do not hard-code software-delivery-only behavior into generic process services.
- Do not mark this subbundle complete without artifact-backed proof under proof/SB08/.

## Acceptance Checklist

- [ ] Non-mutating step with product mutation receipt is flagged.
- [ ] Wrong-root artifact is flagged.
- [ ] Missing lineage is flagged for evidence/deliverable artifacts.
- [ ] Old shallow behavior fails or is red-team documented.
- [ ] New production behavior passes through runtime code, not prompt text.
- [ ] Proof manifest and semantic invariants cite existing artifacts.
- [ ] No SQLite runtime reintroduction.

## Proof Required

Update:

- proof/SB08/manifest.md
- proof/SB08/semantic-invariants.md
- proof/SB08/transcripts/failing-first.txt
- proof/SB08/transcripts/passing.txt
- proof/SB08/transcripts/source-assertions.txt
- proof/SB08/transcripts/anti-stub-audit.txt
- proof/SB08/transcripts/changed-file-hashes.txt

## Browser Validation Logging

- Required if process health dashboard UI changes.
- Add a row in reviews/01-execution-report.md while validation is fresh.

## Progression Gate

- Entry gate must confirm prerequisites and exact source references still match the repo.
- Closure gate must confirm tests, source assertions, anti-stub audit, changed-file hashes, and proof manifest are complete.
- Downstream subbundles must re-check this gate if later observations weaken the proof.

## Suggested Agent Prompt

Implement SB08 from codex/bundles/processes-hardening-followup-runtime-governance-v5. Preserve generic process semantics, keep Processes above Workflows, and capture artifact-backed proof before moving on.
