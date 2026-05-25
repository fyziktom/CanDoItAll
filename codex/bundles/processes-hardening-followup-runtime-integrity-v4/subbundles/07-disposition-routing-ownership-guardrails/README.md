# SB07 — Prevent negative branch routing from masking own artifact failures

## Status

Completed.

## Objective

Classify artifact failures by ownership and allow branch routing only for review/approval disposition failures, not own artifact production failures.

## Covered Inputs

- `analysis/02-verified-findings.md`
- `requirements/01-normalized-requirements.md`

## Prerequisites

- Previous subbundle gates must pass when this subbundle depends on their runtime state.
- Work from branch `processes-hardening`.

## Exact Source References

- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.StepCompletionFinalizer.cs`

## Scope

- Implement the production runtime change, add failing-first/red-team tests, add passing proof, and update proof manifest.

## Dependency Impact

- This subbundle is critical. Downstream subbundles must not assume runtime integrity until this gate passes.

## Validation Depth

- Focused unit or integration tests.
- Source assertions.
- Anti-stub audit.
- Changed-file hashes.
- Full build before final closure.

## Implementation Steps

1. Add failure ownership enum: OwnOutput, UpstreamInput, RuntimeEvidence, ReviewDisposition.
2. OwnOutput Missing/InvalidFormat must block or recover unless explicit policy allows route.
3. Review/QA can route defects to repair/no-go when required decision artifact exists.
4. Add tests for artifact-producing step with negative branch but missing own artifact.

## Scope Exceptions

Do not implement unrelated process UI redesign unless the subbundle explicitly requires editor changes.

## Do Not Do

- Do not add SQLite support.
- Do not confuse workflow executor state with process-owned finalization.
- Do not replace runtime enforcement with prompt-only wording.
- Do not hard-code Blazor/.NET-only behavior unless the test fixture explicitly targets software delivery.

## Acceptance Checklist

- [x] Failing-first or red-team test demonstrates the old failure mode.
- [x] Production code fixes the failure mode.
- [x] Passing test covers the production path.
- [x] No new source-only or prose-only proof.
- [x] New durable state has producer/consumer/lifecycle proof.

## Proof Required

Update:

- `proof/SB07/manifest.md`
- `proof/SB07/semantic-invariants.md`
- `proof/SB07/transcripts/failing-first.txt`
- `proof/SB07/transcripts/passing.txt`
- `proof/SB07/transcripts/source-assertions.txt`
- `proof/SB07/transcripts/anti-stub-audit.txt`
- `proof/SB07/transcripts/changed-file-hashes.txt`

## Browser Validation Logging

- N/A unless this subbundle changes process editor UI or launches browser-visible red-team scenarios.

## Progression Gate

- Do not continue until focused tests pass and the proof manifest is updated.

## Suggested Agent Prompt

Implement SB07 from `codex/bundles/processes-hardening-followup-runtime-integrity-v4`. Preserve generic process semantics and capture artifact-backed proof before moving on.
