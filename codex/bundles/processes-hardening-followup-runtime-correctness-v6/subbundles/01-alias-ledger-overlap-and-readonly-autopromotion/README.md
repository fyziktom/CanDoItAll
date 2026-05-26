# SB01: Fix writable/read-only alias overlap and make prompt-grounding merge safe.

## Objective

Fix writable/read-only alias overlap and make prompt-grounding merge safe.

## Why This Matters

This subbundle closes a concrete runtime correctness gap observed after phase5. The process runtime must avoid both false completion and unnecessary blocking while staying generic.

## Implementation Tasks

- Add tests where an alias is already in `AllowedExternalTargetAliases` and prompt grounding sees the same alias; it must not appear in read-only aliases.
- Add tests for child alias covered by writable parent and sibling alias outside writable parent.
- Modify `GroundPromptExternalTargetAliases` and metadata merge helpers to remove writable-covered aliases from read-only list.
- Ensure `EvaluateReadOnlyExternalTargetMutation` does not deny aliases that are explicitly writable from a trusted ledger source.
- Add a source assertion proving the ledger/writable authority wins over prompt-only read-only discovery.

## Required Tests

- Add failing-first or red-team tests before the production fix where practical.
- Add positive tests proving the fixed behavior.
- Include at least one generic/non-software case if this subbundle changes generic process semantics.

## Closure Criteria

- Production code implements the behavior; no prompt-only fix.
- Proof manifest is updated.
- Focused tests pass.
- No SQLite runtime/migration dependency is introduced.

## Status

- Completed

## Covered Inputs

- RN01 block unnecessarily.
- RN03 deny legitimate product mutation due to read-only alias overlap.
- RQ01 alias read-only/writable overlap.
- RQ06 authoritative grounding ledger.

## Prerequisites

- Prepared-stage bundle validator passes after structural repair.
- Current branch remains `processes-hardening`.
- No prior subbundle is required.

## Exact Source References

- repo://src/CanDoItAll.AgentFramework.Core/Execution/ExecutionInvocationMetadata.cs
- repo://src/CanDoItAll.AgentFramework.Core/ToolPolicy/AgentToolInvocationPolicy.cs
- repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ExecutionMetadata.cs
- repo://tests/CanDoItAll.Tests.Unit/AgentWorkspaceToolAccessMetadataTests.cs
- repo://tests/CanDoItAll.Tests.Unit/AgentToolInvocationPolicyTests.cs

## Deliverables

- Tests for same alias, child-covered alias, and sibling/parent outside writable root.
- Production merge logic that removes writable-covered aliases from the read-only set.
- Policy assertion that trusted writable aliases win over prompt-only read-only discovery.

## Dependency Impact

- SB04 depends on this as a metadata grounding foundation.
- SB06 and SB07 depend on accurate target authority before script and policy extraction.

## Validation Depth

- Focused unit tests for metadata grounding and tool policy.
- Source assertion transcript for ledger/writable authority.
- Anti-stub audit for prompt-only or fixture-specific bypasses.

## Implementation Steps

- Add failing-first or red-team coverage for writable/read-only overlap.
- Normalize external target aliases before merging prompt-grounded aliases.
- Remove any read-only alias that is equal to or covered by a trusted writable alias.
- Update read-only mutation policy so trusted writable roots are not denied.
- Record proof under `bundle://proof/SB01/`.

## Do Not Do

- Do not make prompt aliases writable unless a trusted ledger or operation contract allows mutation.
- Do not add software-project-specific path rules.
- Do not introduce SQLite runtime or migration paths.

## Acceptance Checklist

- Same alias does not appear in both writable and read-only metadata.
- Writable parent covers read-only child discovery.
- Sibling or parent outside writable root stays read-only or denied.
- Focused tests pass.

## Proof Required

- `bundle://proof/SB01/manifest.md`
- `bundle://proof/SB01/semantic-invariants.md`
- Failing-first or red-team transcript.
- Passing focused test transcript.
- Changed-file SHA-256 transcript.
- Anti-stub audit transcript.

## Browser Validation Logging

- N/A: SB01 changes runtime metadata and policy only.

## Progression Gate

- Passed. SB04 may rely on SB01 proof that prompt grounding removes writable-covered read-only aliases and read-only mutation policy honors trusted writable aliases.

## Suggested Agent Prompt

- Implement SB01 exactly as scoped, preserve generic process semantics, update `proof/SB01`, run focused tests, and record the subbundle gate result.
