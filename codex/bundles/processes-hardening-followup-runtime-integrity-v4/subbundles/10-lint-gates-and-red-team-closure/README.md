# SB10 — Strengthen lint gates and run generic red-team closure

## Status

Ready.

## Objective

Apply strict lint based on risk and validate software plus non-software scenarios.

## Covered Inputs

- `analysis/02-verified-findings.md`
- `requirements/01-normalized-requirements.md`

## Prerequisites

- Previous subbundle gates must pass when this subbundle depends on their runtime state.
- Work from branch `processes-hardening`.

## Exact Source References

- `repo://src/CanDoItAll.Modules.Processes/Definitions/ProcessDefinitionLinter.cs`
- `repo://src/CanDoItAll.Modules.Processes/Services/ProcessesService.Publication.cs`
- `repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessesService.Runtime.RunStart.cs`

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

1. Make strict lint automatic for high-criticality/autonomous definitions.
2. Expose all lint issues in UI or a details drawer.
3. Add red-team cases for architecture-not-implementation, business artifact destination, legal review, manufacturing QA, and research report.
4. Run full validation and update execution report.

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

- `proof/SB10/manifest.md`
- `proof/SB10/semantic-invariants.md`
- `proof/SB10/transcripts/failing-first.txt`
- `proof/SB10/transcripts/passing.txt`
- `proof/SB10/transcripts/source-assertions.txt`
- `proof/SB10/transcripts/anti-stub-audit.txt`
- `proof/SB10/transcripts/changed-file-hashes.txt`

## Browser Validation Logging

N/A unless this subbundle changes process editor UI or launches browser-visible red-team scenarios.

## Progression Gate

Do not continue until focused tests pass and the proof manifest is updated.

## Suggested Agent Prompt

Implement SB10 from `codex/bundles/processes-hardening-followup-runtime-integrity-v4`. Preserve generic process semantics and capture artifact-backed proof before moving on.
