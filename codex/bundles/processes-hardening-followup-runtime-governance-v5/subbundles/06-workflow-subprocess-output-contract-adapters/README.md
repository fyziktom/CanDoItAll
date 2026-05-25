# SB06 - Workflow/Subprocess Output Contract Adapters

## Status


- Completed

## Objective

Replace loose kind/title/summary matching with explicit output mapping for workflow and subprocess roles.

## Covered Inputs

- RQ06
- VF08
- N003
- N005

## Prerequisites

- SB01-SB05 closure gates passed.
- Projection identity and storage-backed validation are available for mapped artifacts.

## Exact Source References

- repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessWorkflowRunCoordinator.cs
- repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Dispatch.cs
- repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ArtifactProjection.cs
- repo://src/CanDoItAll.Modules.Processes/Definitions/ProcessDefinitionLinter.cs
- repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs
- repo://tests/CanDoItAll.Tests.Integration/ProcessWorkflowExecutorIntegrationTests.cs
- repo://tests/CanDoItAll.Tests.Integration/ProcessSubprocessIntegrationTests.cs

## Scope

- Workflow executor output mapping model.
- Subprocess child-to-parent artifact mapping model.
- Publish/start lint validation for missing or ambiguous mappings.
- Projection/finalizer usage of explicit mappings over loose same-kind/title matching.

## Dependency Impact

- Critical subbundle.
- SB07 recovery and SB08 invariant audit depend on explicit artifact mapping semantics.

## Validation Depth

- Failing-first or red-team test transcript.
- Focused unit/integration tests named in the proof manifest.
- Source assertions against production code paths.
- Anti-stub audit.
- Changed-file SHA-256 hashes.
- Full build and PostgreSQL-only audit before final closure.

## Implementation Steps

- Add red-team tests for same-kind wrong-title workflow artifacts and sibling child artifacts.
- Add explicit mapping contracts to process role/assignment or artifact expectation metadata.
- Use mappings in workflow and subprocess projection adapters.
- Update proof artifacts after focused mapping tests pass.

## Do Not Do

- Do not add SQLite support or SQLite-specific runtime/migration paths.
- Do not confuse workflow executor state with process-owned lifecycle, finalization, or governance.
- Do not replace runtime enforcement with prompt-only wording.
- Do not hard-code software-delivery-only behavior into generic process services.
- Do not mark this subbundle complete without artifact-backed proof under proof/SB06/.

## Acceptance Checklist

- [ ] Two same-kind workflow artifacts do not bind to the wrong expectation.
- [ ] Subprocess child artifact maps only through declared mapping.
- [ ] Missing mapping produces lint error or blocked start depending mode.
- [ ] Old shallow behavior fails or is red-team documented.
- [ ] New production behavior passes through runtime code, not prompt text.
- [ ] Proof manifest and semantic invariants cite existing artifacts.
- [ ] No SQLite runtime reintroduction.

## Proof Required

Update:

- proof/SB06/manifest.md
- proof/SB06/semantic-invariants.md
- proof/SB06/transcripts/failing-first.txt
- proof/SB06/transcripts/passing.txt
- proof/SB06/transcripts/source-assertions.txt
- proof/SB06/transcripts/anti-stub-audit.txt
- proof/SB06/transcripts/changed-file-hashes.txt

## Browser Validation Logging

- N/A unless mapping editor UI is added.
- Add a row in reviews/01-execution-report.md while validation is fresh.

## Progression Gate

- Entry gate must confirm prerequisites and exact source references still match the repo.
- Closure gate must confirm tests, source assertions, anti-stub audit, changed-file hashes, and proof manifest are complete.
- Downstream subbundles must re-check this gate if later observations weaken the proof.

## Suggested Agent Prompt

Implement SB06 from codex/bundles/processes-hardening-followup-runtime-governance-v5. Preserve generic process semantics, keep Processes above Workflows, and capture artifact-backed proof before moving on.
