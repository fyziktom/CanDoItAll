# SB01 - Persisted Step Operation Contract

## Status


- Completed

## Objective

Add first-class persisted operation-contract fields to process step definitions, editor models, import/export, templates, and UI.

## Covered Inputs

- RQ01
- VF02
- N002
- N004

## Prerequisites

- None. This is the first authorization-foundation subbundle.
- Verify the current working copy already contains the phase4 process hardening baseline.

## Exact Source References

- repo://src/CanDoItAll.Modules.Processes/Definitions/ProcessDefinitionEnums.cs
- repo://src/CanDoItAll.Modules.Processes/Definitions/ProcessDefinitionEditorModels.cs
- repo://src/CanDoItAll.Modules.Processes/Persistence/Entities/ProcessDefinitionEntities.cs
- repo://src/CanDoItAll.Modules.Processes/Persistence/Configurations/ProcessDefinitionEntityConfigurations.cs
- repo://src/CanDoItAll.Modules.Processes/Components/ProcessStepEditorForm.razor
- repo://src/CanDoItAll.Modules.Processes/ImportExport/ProcessImportExportModels.cs
- repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ExecutionMetadata.cs
- repo://tests/CanDoItAll.Tests.Integration/ProcessesServiceIntegrationTests.cs
- repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs
- repo://tests/CanDoItAll.Tests.Components/ProcessStepEditorFormTests.cs

## Scope

- Persisted allowed-operation and target-scope fields on process step definitions.
- Editor, import/export, templates, clone, and runtime dispatch read/write support.
- Heuristic parser retained only as migration/backward-compatible fallback.

## Dependency Impact

- Critical subbundle.
- SB02 and SB03 depend on this typed contract metadata.

## Validation Depth

- Failing-first or red-team test transcript.
- Focused unit/integration tests named in the proof manifest.
- Source assertions against production code paths.
- Anti-stub audit.
- Changed-file SHA-256 hashes.
- Full build and PostgreSQL-only audit before final closure.

## Implementation Steps

- Add failing or red-team coverage for text-driven misclassification and import/export loss.
- Wire persisted operation contract fields through persistence, editor models, import/export, templates, and dispatch.
- Validate business-plan artifact-only and software implementation mutation scenarios.
- Update proof artifacts after passing focused tests.

## Do Not Do

- Do not add SQLite support or SQLite-specific runtime/migration paths.
- Do not confuse workflow executor state with process-owned lifecycle, finalization, or governance.
- Do not replace runtime enforcement with prompt-only wording.
- Do not hard-code software-delivery-only behavior into generic process services.
- Do not mark this subbundle complete without artifact-backed proof under proof/SB01/.

## Acceptance Checklist

- [ ] A business-plan report step with words create/generate stays artifact-only.
- [ ] A software implementation step explicitly allows MutateProductTarget.
- [ ] Imported/exported definitions preserve operations and target scope.
- [ ] Old shallow behavior fails or is red-team documented.
- [ ] New production behavior passes through runtime code, not prompt text.
- [ ] Proof manifest and semantic invariants cite existing artifacts.
- [ ] No SQLite runtime reintroduction.

## Proof Required

Update:

- proof/SB01/manifest.md
- proof/SB01/semantic-invariants.md
- proof/SB01/transcripts/failing-first.txt
- proof/SB01/transcripts/passing.txt
- proof/SB01/transcripts/source-assertions.txt
- proof/SB01/transcripts/anti-stub-audit.txt
- proof/SB01/transcripts/changed-file-hashes.txt

## Browser Validation Logging

- Required if process step editor UI changes; otherwise N/A.
- Add a row in reviews/01-execution-report.md while validation is fresh.

## Progression Gate

- Entry gate must confirm prerequisites and exact source references still match the repo.
- Closure gate must confirm tests, source assertions, anti-stub audit, changed-file hashes, and proof manifest are complete.
- Downstream subbundles must re-check this gate if later observations weaken the proof.

## Suggested Agent Prompt

Implement SB01 from codex/bundles/processes-hardening-followup-runtime-governance-v5. Preserve generic process semantics, keep Processes above Workflows, and capture artifact-backed proof before moving on.
