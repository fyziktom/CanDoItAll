# SB02 — Move recovery/workflow/subprocess lineage out of bounded external reference keys

## Status

Ready.

## Objective

Introduce typed artifact provenance payload/table or structured JSON and compact hash keys so lineage cannot be truncated.

## Covered Inputs

- `analysis/02-verified-findings.md`
- `requirements/01-normalized-requirements.md`

## Prerequisites

- Previous subbundle gates must pass when this subbundle depends on their runtime state.
- Work from branch `processes-hardening`.

## Exact Source References

- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ArtifactProjection.cs`
- `repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessesService.Runtime.Operations.cs`

## Scope

Implement the production runtime change, add failing-first/red-team tests, add passing proof, and update proof manifest.

## Dependency Impact

This subbundle is critical. Downstream subbundles must not assume runtime integrity until this gate passes.

## Validation Depth

- Focused unit or integration tests.
- Source assertions.
- Anti-stub audit.
- Changed-file hashes.
- Full build before final closure.

## Implementation Steps

1. Create compact external reference key hash for uniqueness.
2. Persist recovery/workflow/subprocess/source artifact lineage in structured payload.
3. Update `ResolveArtifactProducerKind` and `IsCurrentRunArtifact` to use typed lineage before text/provenance fallback.
4. Add tests with long lineage that exceeds 200 chars and still validates correctly.

## Scope Exceptions

Do not implement unrelated process UI redesign unless the subbundle explicitly requires editor changes.

## Do Not Do

- Do not add SQLite support.
- Do not confuse workflow executor state with process-owned finalization.
- Do not replace runtime enforcement with prompt-only wording.
- Do not hard-code Blazor/.NET-only behavior unless the test fixture explicitly targets software delivery.

## Acceptance Checklist

- [ ] Failing-first or red-team test demonstrates the old failure mode.
- [ ] Production code fixes the failure mode.
- [ ] Passing test covers the production path.
- [ ] No new source-only or prose-only proof.
- [ ] New durable state has producer/consumer/lifecycle proof.

## Proof Required

Update:

- `proof/SB02/manifest.md`
- `proof/SB02/semantic-invariants.md`
- `proof/SB02/transcripts/failing-first.txt`
- `proof/SB02/transcripts/passing.txt`
- `proof/SB02/transcripts/source-assertions.txt`
- `proof/SB02/transcripts/anti-stub-audit.txt`
- `proof/SB02/transcripts/changed-file-hashes.txt`

## Browser Validation Logging

N/A unless this subbundle changes process editor UI or launches browser-visible red-team scenarios.

## Progression Gate

Do not continue until focused tests pass and the proof manifest is updated.

## Suggested Agent Prompt

Implement SB02 from `codex/bundles/processes-hardening-followup-runtime-integrity-v4`. Preserve generic process semantics and capture artifact-backed proof before moving on.
