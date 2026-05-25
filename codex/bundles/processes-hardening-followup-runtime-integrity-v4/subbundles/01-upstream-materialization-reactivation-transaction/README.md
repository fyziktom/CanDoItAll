# SB01 — Make upstream artifact materialization reactivation transaction-safe

## Status

Completed.

## Objective

Fix `RecordArtifactAsync` / `ReactivateBlockedDownstreamStepsAfterArtifactMaterializationAsync` so the artifact being recorded is included in dependent-step satisfaction before the transaction commits.

## Covered Inputs

- `analysis/02-verified-findings.md`
- `requirements/01-normalized-requirements.md`

## Prerequisites

- Previous subbundle gates must pass when this subbundle depends on their runtime state.
- Work from branch `processes-hardening`.

## Exact Source References

- `repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessesService.Runtime.Operations.cs`
- `repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessRuntimeProgressionPlanner.cs`

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

1. Add failing test where downstream step is blocked for missing upstream artifact, upstream artifact is recorded, and downstream reopens in the same call.
2. Refactor reactivation to accept the newly added artifact or save/flush before reactivation inside the same transaction.
3. Ensure reactivation creates journal entry and outbox/observation notification for redispatch.
4. Do not rely on BlockedReason string only; add typed journal/reason helper if needed.

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

- `proof/SB01/manifest.md`
- `proof/SB01/semantic-invariants.md`
- `proof/SB01/transcripts/failing-first.txt`
- `proof/SB01/transcripts/passing.txt`
- `proof/SB01/transcripts/source-assertions.txt`
- `proof/SB01/transcripts/anti-stub-audit.txt`
- `proof/SB01/transcripts/changed-file-hashes.txt`

## Browser Validation Logging

- N/A unless this subbundle changes process editor UI or launches browser-visible red-team scenarios.

## Progression Gate

- Do not continue until focused tests pass and the proof manifest is updated.
- Closure result: passed. SB02 may rely on upstream materialization reactivation including the tracked artifact recorded in the same `RecordArtifactAsync` call.

## Suggested Agent Prompt

Implement SB01 from `codex/bundles/processes-hardening-followup-runtime-integrity-v4`. Preserve generic process semantics and capture artifact-backed proof before moving on.
