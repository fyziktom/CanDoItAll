# SB09 — Persist no-progress retry fingerprints and reconcile active execution runs

## Status

Completed.

## Objective

Make no-progress detection survive dispatcher restarts and avoid adopting stale/wrong active runs.

## Covered Inputs

- `analysis/02-verified-findings.md`
- `requirements/01-normalized-requirements.md`

## Prerequisites

- Previous subbundle gates must pass when this subbundle depends on their runtime state.
- Work from branch `processes-hardening`.

## Exact Source References

- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Concurrency.cs`
- `repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessRuntimeEventTypes.cs`

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

1. Create durable no-progress fingerprint journal/table.
2. Include execution run id, tool signature, artifact validation fingerprint, mutation delta, and proof delta.
3. Stop retries when same fingerprint repeats without new evidence.
4. Audit active run adoption against current dispatch claim/window.
5. Add tests across simulated service restart.

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

- `proof/SB09/manifest.md`
- `proof/SB09/semantic-invariants.md`
- `proof/SB09/transcripts/failing-first.txt`
- `proof/SB09/transcripts/passing.txt`
- `proof/SB09/transcripts/source-assertions.txt`
- `proof/SB09/transcripts/anti-stub-audit.txt`
- `proof/SB09/transcripts/changed-file-hashes.txt`

## Browser Validation Logging

- N/A unless this subbundle changes process editor UI or launches browser-visible red-team scenarios.

## Progression Gate

- Do not continue until focused tests pass and the proof manifest is updated.

## Suggested Agent Prompt

Implement SB09 from `codex/bundles/processes-hardening-followup-runtime-integrity-v4`. Preserve generic process semantics and capture artifact-backed proof before moving on.
