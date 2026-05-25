# SB03 — Close script/run tool boundary bypasses

## Status

Completed.

## Objective

Ensure non-mutating process steps cannot mutate targets via helper scripts, run tools, or hidden script side effects.

## Covered Inputs

- `analysis/02-verified-findings.md`
- `requirements/01-normalized-requirements.md`

## Prerequisites

- Previous subbundle gates must pass when this subbundle depends on their runtime state.
- Work from branch `processes-hardening`.

## Exact Source References

- `repo://src/CanDoItAll.AgentFramework.Core/ToolPolicy/AgentToolInvocationPolicy.cs`
- `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/MafAgentRuntime.AgentFactory.cs`

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

1. Classify script tools against process operations and product mutation metadata.
2. For non-mutating steps, allow scripts only when target is current-run artifact root or read-only validation without writes.
3. Inspect script path/content when possible before execution.
4. Deny scripts containing grounded mutable product target paths when product mutation is false.
5. Add red-team tests for PowerShell/Python helpers that attempt product writes.

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
- [x] New durable state has producer/consumer/lifecycle proof. N/A: no durable state introduced.

## Proof Required

Update:

- `proof/SB03/manifest.md`
- `proof/SB03/semantic-invariants.md`
- `proof/SB03/transcripts/failing-first.txt`
- `proof/SB03/transcripts/passing.txt`
- `proof/SB03/transcripts/source-assertions.txt`
- `proof/SB03/transcripts/anti-stub-audit.txt`
- `proof/SB03/transcripts/changed-file-hashes.txt`

## Browser Validation Logging

- N/A unless this subbundle changes process editor UI or launches browser-visible red-team scenarios.

## Progression Gate

- Closed. Focused SB03 tests pass, `AgentToolInvocationPolicyTests` regression sweep passes, and `proof/SB03/manifest.md` is updated.

## Suggested Agent Prompt

Implement SB03 from `codex/bundles/processes-hardening-followup-runtime-integrity-v4`. Preserve generic process semantics and capture artifact-backed proof before moving on.
