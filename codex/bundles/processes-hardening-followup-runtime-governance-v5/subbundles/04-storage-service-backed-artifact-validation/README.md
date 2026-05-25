# SB04 - Storage-Backed Artifact Validation

## Status


- Completed

## Objective

Validate artifact content through storage abstractions instead of assuming workspace filesystem paths.

## Covered Inputs

- RQ04
- VF06
- N001
- N005

## Prerequisites

- SB01-SB03 closure gates passed.
- Trusted target grounding and operation policy cannot be weakened by artifact validation fallback.

## Exact Source References

- repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ArtifactValidation.cs
- repo://src/CanDoItAll.Infrastructure/Storage/Abstractions/StorageContracts.cs
- repo://src/CanDoItAll.Infrastructure/Storage/Access/StorageAccessService.cs
- repo://src/CanDoItAll.Infrastructure/Storage/Drivers/StorageDriverRegistry.cs
- repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs
- repo://tests/CanDoItAll.Tests.Unit/StorageAccessServiceTests.cs

## Scope

- IProcessArtifactContentResolver or equivalent storage-backed artifact content abstraction.
- Workspace reader retained as one resolver implementation.
- Validation paths that read stored bytes for managed storage artifacts and do not reject valid non-workspace storage only because it lacks a workspace path.

## Dependency Impact

- Critical subbundle.
- SB05 and SB06 rely on validated artifact bytes and typed storage provenance.

## Validation Depth

- Failing-first or red-team test transcript.
- Focused unit/integration tests named in the proof manifest.
- Source assertions against production code paths.
- Anti-stub audit.
- Changed-file SHA-256 hashes.
- Full build and PostgreSQL-only audit before final closure.

## Implementation Steps

- Add failing tests for malformed JSON and non-workspace managed storage artifacts.
- Introduce a resolver abstraction over workspace and storage access services.
- Route finalizer artifact content validation through the resolver.
- Update proof artifacts after focused tests pass.

## Do Not Do

- Do not add SQLite support or SQLite-specific runtime/migration paths.
- Do not confuse workflow executor state with process-owned lifecycle, finalization, or governance.
- Do not replace runtime enforcement with prompt-only wording.
- Do not hard-code software-delivery-only behavior into generic process services.
- Do not mark this subbundle complete without artifact-backed proof under proof/SB04/.

## Acceptance Checklist

- [ ] Malformed JSON in storage is rejected.
- [ ] Valid Markdown stored through managed storage passes.
- [ ] Non-workspace storage artifact can be validated.
- [ ] Old shallow behavior fails or is red-team documented.
- [ ] New production behavior passes through runtime code, not prompt text.
- [ ] Proof manifest and semantic invariants cite existing artifacts.
- [ ] No SQLite runtime reintroduction.

## Proof Required

Update:

- proof/SB04/manifest.md
- proof/SB04/semantic-invariants.md
- proof/SB04/transcripts/failing-first.txt
- proof/SB04/transcripts/passing.txt
- proof/SB04/transcripts/source-assertions.txt
- proof/SB04/transcripts/anti-stub-audit.txt
- proof/SB04/transcripts/changed-file-hashes.txt

## Browser Validation Logging

- N/A unless artifact validation results become browser-visible.
- Add a row in reviews/01-execution-report.md while validation is fresh.

## Progression Gate

- Entry gate must confirm prerequisites and exact source references still match the repo.
- Closure gate must confirm tests, source assertions, anti-stub audit, changed-file hashes, and proof manifest are complete.
- Downstream subbundles must re-check this gate if later observations weaken the proof.

## Suggested Agent Prompt

Implement SB04 from codex/bundles/processes-hardening-followup-runtime-governance-v5. Preserve generic process semantics, keep Processes above Workflows, and capture artifact-backed proof before moving on.
