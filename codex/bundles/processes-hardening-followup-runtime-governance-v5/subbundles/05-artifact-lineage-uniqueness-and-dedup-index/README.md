# SB05 - Artifact Lineage Uniqueness and Dedup Index

## Status


- Completed

## Objective

Use stable typed lineage identity for artifact dedupe and audit rather than bounded ExternalReferenceKey.

## Covered Inputs

- RQ05
- VF07
- N001
- N005

## Prerequisites

- SB01-SB04 closure gates passed.
- Storage-backed artifact validation can prove artifact bytes independently of display keys.

## Exact Source References

- repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessArtifactProjectionLineage.cs
- repo://src/CanDoItAll.Modules.Processes/Persistence/Entities/ProcessRuntimeModels.cs
- repo://src/CanDoItAll.Modules.Processes/Persistence/Configurations/ProcessRuntimeEntityConfigurations.cs
- repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ArtifactProjection.cs
- repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessesService.Runtime.Operations.cs
- repo://tests/CanDoItAll.Tests.Integration/ProcessesServiceIntegrationTests.cs
- repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs

## Scope

- Stable projection identity hash derived from typed lineage.
- Deduplication and uniqueness checks that prefer projection identity over bounded ExternalReferenceKey.
- Compatibility preservation for ExternalReferenceKey as display/debug data.

## Dependency Impact

- Critical subbundle.
- SB06 and SB07 use projection identity to avoid wrong artifact binding during mapping and recovery.

## Validation Depth

- Failing-first or red-team test transcript.
- Focused unit/integration tests named in the proof manifest.
- Source assertions against production code paths.
- Anti-stub audit.
- Changed-file SHA-256 hashes.
- Full build and PostgreSQL-only audit before final closure.

## Implementation Steps

- Add failing tests for long lineage keys and duplicate concurrent projection attempts.
- Add projection identity hash production and persistence.
- Update projection and runtime artifact record paths to dedupe by typed identity.
- Update proof artifacts after focused tests pass.

## Do Not Do

- Do not add SQLite support or SQLite-specific runtime/migration paths.
- Do not confuse workflow executor state with process-owned lifecycle, finalization, or governance.
- Do not replace runtime enforcement with prompt-only wording.
- Do not hard-code software-delivery-only behavior into generic process services.
- Do not mark this subbundle complete without artifact-backed proof under proof/SB05/.

## Acceptance Checklist

- [ ] Long lineage does not collide after key truncation.
- [ ] Manager recovery artifact dedupes correctly.
- [ ] Workflow/subprocess artifacts dedupe by typed source IDs.
- [ ] Old shallow behavior fails or is red-team documented.
- [ ] New production behavior passes through runtime code, not prompt text.
- [ ] Proof manifest and semantic invariants cite existing artifacts.
- [ ] No SQLite runtime reintroduction.

## Proof Required

Update:

- proof/SB05/manifest.md
- proof/SB05/semantic-invariants.md
- proof/SB05/transcripts/failing-first.txt
- proof/SB05/transcripts/passing.txt
- proof/SB05/transcripts/source-assertions.txt
- proof/SB05/transcripts/anti-stub-audit.txt
- proof/SB05/transcripts/changed-file-hashes.txt

## Browser Validation Logging

- N/A unless lineage identity is surfaced in UI.
- Add a row in reviews/01-execution-report.md while validation is fresh.

## Progression Gate

- Entry gate must confirm prerequisites and exact source references still match the repo.
- Closure gate must confirm tests, source assertions, anti-stub audit, changed-file hashes, and proof manifest are complete.
- Downstream subbundles must re-check this gate if later observations weaken the proof.

## Suggested Agent Prompt

Implement SB05 from codex/bundles/processes-hardening-followup-runtime-governance-v5. Preserve generic process semantics, keep Processes above Workflows, and capture artifact-backed proof before moving on.
