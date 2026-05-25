# SB06 — Replace kind/title heuristic mapping for workflow/subprocess outputs

## Status

Completed.

## Objective

Add explicit mapping from workflow/subprocess output ids to process artifact expectations.

## Covered Inputs

- `analysis/02-verified-findings.md`
- `requirements/01-normalized-requirements.md`

## Prerequisites

- Previous subbundle gates must pass when this subbundle depends on their runtime state.
- Work from branch `processes-hardening`.

## Exact Source References

- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessWorkflowRunCoordinator.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Dispatch.cs`

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

1. Add workflow output mapping model or metadata.
2. Add subprocess child expectation -> parent expectation mapping.
3. Block/lint ambiguous many-to-one or one-to-many mappings.
4. Keep heuristic only as single-artifact fallback with diagnostics.
5. Add tests with two same-kind artifacts where heuristic would choose wrong one.

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

- `proof/SB06/manifest.md`
- `proof/SB06/semantic-invariants.md`
- `proof/SB06/transcripts/failing-first.txt`
- `proof/SB06/transcripts/passing.txt`
- `proof/SB06/transcripts/source-assertions.txt`
- `proof/SB06/transcripts/anti-stub-audit.txt`
- `proof/SB06/transcripts/changed-file-hashes.txt`

## Browser Validation Logging

- N/A unless this subbundle changes process editor UI or launches browser-visible red-team scenarios.

## Progression Gate

- Do not continue until focused tests pass and the proof manifest is updated.

## Suggested Agent Prompt

Implement SB06 from `codex/bundles/processes-hardening-followup-runtime-integrity-v4`. Preserve generic process semantics and capture artifact-backed proof before moving on.
