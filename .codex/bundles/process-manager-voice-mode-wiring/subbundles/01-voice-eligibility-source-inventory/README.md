# Voice eligibility source inventory

## Status

- `Completed`

## Objective

- Establish the exact source-level voice eligibility path across normal chat, contextual windows, Manager chat, and provider runtime so implementation cannot produce a shallow button-only fix.

## Success Criteria

- The inventory identifies the disabled-state owner for Manager chat.
- The inventory distinguishes UI eligibility metadata from provider runtime capability.
- Shallow-pass traps and downstream reopen triggers are recorded in proof artifacts.

## Covered Inputs

- N001, N003, N004, N005 source-analysis portions.
- R001, R002, R004 architecture prerequisites.

## Prerequisites

- none.

## Exact Source References

- `repo://src/CanDoItAll.AgentFramework.Components/ChatWorkspacePanel.razor`
- `repo://src/CanDoItAll.Modules.AgentFramework/Pages/Components/AgentChatPanel.razor.cs`
- `repo://src/CanDoItAll.AgentFramework.Components/ContextualAgentWorkspaceWindows.razor`
- `repo://src/CanDoItAll.Modules.Processes/Components/ProcessWorkspaceShell.razor`
- `repo://src/CanDoItAll.AgentFramework.Voice/AgentVoiceService.cs`
- `repo://src/CanDoItAll.AgentFramework.Voice/ProviderRuntimeVoiceDriver.cs`

## Deliverables

- Source inventory updates in `bundle://inventories/01-voice-surface-inventory.md`.
- Proof manifest `bundle://proof/SB01/manifest.md`.
- Semantic invariant contract `bundle://proof/SB01/semantic-invariants.md`.

## Dependency Impact

- SB02 depends on this phase to know which owner should pass voice state/callbacks to `ChatWorkspacePanel`.
- SB03 depends on this phase to know which provider runtime contracts must be proved.
- SB04 depends on this phase for browser assertions and raw-note closure.

## Validation Depth

- Critical foundation with source assertions, semantic adequacy proof, and anti-stub audit.

## Implementation Steps

1. Re-read the exact source references.
2. Confirm where `CanUseVoiceMode` is supplied in normal chat and contextual windows.
3. Confirm Manager chat omits `CanUseVoiceMode` and voice callbacks.
4. Confirm `ProviderRuntimeVoiceDriver` dispatches through typed provider driver interfaces.
5. Capture source assertion transcript and update proof artifacts.

## Scope Exceptions

- This phase does not change production code.

## Do Not Do

- Do not edit production files.
- Do not rely on visual browser proof.

## Acceptance Checklist

- `ChatWorkspacePanel` disabled-state inputs are named.
- Manager chat missing voice inputs/callbacks are named.
- Provider runtime STT/TTS dispatch contracts are named.
- Downstream tests required by SB02 and SB03 are explicit.

## Proof Required

- `bundle://proof/SB01/transcripts/source-assertions.txt`
- `bundle://proof/SB01/transcripts/anti-stub-audit.txt`
- `bundle://proof/SB01/manifest.md`
- `bundle://proof/SB01/semantic-invariants.md`
- Closure captured in `bundle://proof/SB01/manifest.md` and `bundle://proof/SB01/semantic-invariants.md`.

## Browser Validation Logging

- N/A. This subbundle is source inventory only.

## Progression Gate

- Downstream work may continue only after source assertions prove Manager chat omits voice wiring and provider runtime contracts are identified.

## Suggested Agent Prompt

```text
Implement this subbundle only.
Work outcome-first: preserve the listed scope boundaries, verify prerequisites before editing, make the smallest correct change set, capture the required proof, update the execution report rows, and stop if the progression gate cannot honestly pass.
```
