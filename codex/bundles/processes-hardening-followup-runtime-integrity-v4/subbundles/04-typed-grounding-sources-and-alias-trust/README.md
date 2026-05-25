# SB04 — Replace free-text alias promotion with typed grounding source authority

## Status

Completed.

## Objective

Add typed target grounding records and restrict writable alias promotion to trusted current-run source kinds.

## Covered Inputs

- `analysis/02-verified-findings.md`
- `requirements/01-normalized-requirements.md`

## Prerequisites

- Previous subbundle gates must pass when this subbundle depends on their runtime state.
- Work from branch `processes-hardening`.

## Exact Source References

- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ExecutionMetadata.cs`
- `repo://src/CanDoItAll.AgentFramework.Core/Execution/ExecutionInvocationMetadata.cs`

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

1. Introduce `ProcessTargetGroundingRecord` or equivalent internal model.
2. Separate `TextMention` from trusted grounding.
3. Allow writable aliases only from project structure current-run, launch plan, or explicit step contract.
4. Treat upstream artifact/provenance aliases as read-only unless explicitly promoted.
5. Add tests where stale upstream summary mentions sibling path and ensure it is not writable.

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
- [x] New durable state has producer/consumer/lifecycle proof. N/A: no new durable state; transient metadata lifecycle is captured in the manifest matrix.

## Proof Required

Update:

- `proof/SB04/manifest.md`
- `proof/SB04/semantic-invariants.md`
- `proof/SB04/transcripts/failing-first.txt`
- `proof/SB04/transcripts/passing.txt`
- `proof/SB04/transcripts/source-assertions.txt`
- `proof/SB04/transcripts/anti-stub-audit.txt`
- `proof/SB04/transcripts/changed-file-hashes.txt`

## Browser Validation Logging

- N/A unless this subbundle changes process editor UI or launches browser-visible red-team scenarios.

## Progression Gate

- Closed. Focused tests pass and the proof manifest is updated.

## Suggested Agent Prompt

Implement SB04 from `codex/bundles/processes-hardening-followup-runtime-integrity-v4`. Preserve generic process semantics and capture artifact-backed proof before moving on.
