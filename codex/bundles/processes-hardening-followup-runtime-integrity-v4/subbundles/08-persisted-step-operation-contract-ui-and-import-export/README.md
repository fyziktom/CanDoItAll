# SB08 — Make operation contract first-class persisted data

## Status

Completed.

## Objective

Move operation contract from text parsing to typed step fields with editor/import/export support.

## Covered Inputs

- `analysis/02-verified-findings.md`
- `requirements/01-normalized-requirements.md`

## Prerequisites

- Previous subbundle gates must pass when this subbundle depends on their runtime state.
- Work from branch `processes-hardening`.

## Exact Source References

- `repo://src/CanDoItAll.Modules.Processes/Definitions`
- `repo://src/CanDoItAll.Modules.Processes/Components/ProcessDefinitionForm.razor`

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

1. Add persisted fields or owned JSON for allowed operations and target scope.
2. Update editor model, save/load, import/export, and UI selectors.
3. Use text parser only as migration/backfill fallback.
4. Add linter issue for inferred low-confidence contract.
5. Add component tests if UI changes.

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

- `proof/SB08/manifest.md`
- `proof/SB08/semantic-invariants.md`
- `proof/SB08/transcripts/failing-first.txt`
- `proof/SB08/transcripts/passing.txt`
- `proof/SB08/transcripts/source-assertions.txt`
- `proof/SB08/transcripts/anti-stub-audit.txt`
- `proof/SB08/transcripts/changed-file-hashes.txt`

## Browser Validation Logging

- N/A unless this subbundle changes process editor UI or launches browser-visible red-team scenarios.

## Progression Gate

- Do not continue until focused tests pass and the proof manifest is updated.

## Suggested Agent Prompt

Implement SB08 from `codex/bundles/processes-hardening-followup-runtime-integrity-v4`. Preserve generic process semantics and capture artifact-backed proof before moving on.
