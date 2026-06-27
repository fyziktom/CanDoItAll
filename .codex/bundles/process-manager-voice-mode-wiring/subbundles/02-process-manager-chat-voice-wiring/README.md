# Process manager chat voice wiring

## Status

- `Completed`

## Objective

- Wire Processes Manager chat to the shared `ChatWorkspacePanel` voice inputs and callbacks so voice-enabled manager agents get enabled controls and voice-disabled agents remain disabled.

## Success Criteria

- Failing-first component test proves current Manager chat voice buttons are disabled for a voice-enabled manager agent.
- Passing component test proves voice-enabled Manager chat renders enabled voice controls.
- Negative component test proves voice-disabled Manager chat still disables controls.
- Source assertions prove Manager chat callbacks use `IAgentVoiceService` and browser voice JS interop.

## Covered Inputs

- N001, N003, N004.
- R001, R002, R003, R006.

## Prerequisites

- SB01 closure gate passed.

## Exact Source References

- `repo://src/CanDoItAll.Modules.Processes/Components/ProcessWorkspaceShell.razor`
- `repo://src/CanDoItAll.AgentFramework.Components/ChatWorkspacePanel.razor`
- `repo://src/CanDoItAll.Modules.AgentFramework/Pages/Components/AgentChatPanel.razor.cs`
- `repo://src/CanDoItAll.AgentFramework.Components/ContextualAgentWorkspaceWindows.razor`
- `repo://tests/CanDoItAll.Tests.Components/ProcessWorkspaceShellTests.cs`
- `repo://tests/CanDoItAll.Tests.Components/ChatWorkspacePanelTests.cs`

## Deliverables

- Manager chat voice state fields and callbacks in `ProcessWorkspaceShell.razor`.
- `ChatWorkspacePanel` Manager chat invocation supplies `CanUseVoiceMode`, voice state/status, and voice callbacks.
- Focused tests in `ProcessWorkspaceShellTests.cs`.
- `bundle://proof/SB02/manifest.md` and `bundle://proof/SB02/semantic-invariants.md`.

## Dependency Impact

- SB04 browser proof depends on this phase for visible enabled controls.
- Provider proof in SB03 does not close the user-visible defect unless this phase passes.

## Validation Depth

- Critical UI foundation with failing-first, semantic positive, adversarial negative, source assertions, and anti-stub audit.

## Implementation Steps

1. Add failing-first bUnit coverage for Manager chat with a voice-enabled manager agent.
2. Add manager-chat voice state and status fields.
3. Read selected manager-agent voice access using `AgentVoiceAccessMetadata.Read(managerChatAgent.ConfigurationJson)`.
4. Pass all voice parameters and callbacks to `ChatWorkspacePanel`.
5. Implement manager-chat record, transcribe, speak, and mode callbacks using `IAgentVoiceService` and JS interop.
6. Reset manager-chat voice recording/mode state when selected manager agent no longer allows voice.
7. Add passing positive and disabled-agent negative tests.

## Scope Exceptions

- Live browser proof is deferred to SB04.
- Provider runtime driver proof is deferred to SB03.

## Do Not Do

- Do not duplicate voice button markup outside `ChatWorkspacePanel`.
- Do not make UI code call OpenAI or provider endpoints directly.
- Do not enable voice for agents whose voice access metadata denies it.
- Do not alter process prompt context semantics except where voice transcription populates the draft prompt.

## Acceptance Checklist

- Voice-enabled Manager chat buttons are enabled.
- Voice-disabled Manager chat buttons are disabled.
- Toggling voice mode updates Manager chat audio status.
- Recording callback invokes browser recording JS and transcription service.
- Speak callback invokes synthesis service and audio queue JS.
- Existing Manager chat send/approval/session tests still pass.

## Proof Required

- `bundle://proof/SB02/transcripts/failing-first-manager-chat-voice.txt`
- `bundle://proof/SB02/transcripts/passing-manager-chat-voice.txt`
- `bundle://proof/SB02/transcripts/source-assertions.txt`
- `bundle://proof/SB02/transcripts/anti-stub-audit.txt`
- `bundle://proof/SB02/manifest.md`
- `bundle://proof/SB02/semantic-invariants.md`
- Closure captured in `bundle://proof/SB02/manifest.md` and `bundle://proof/SB02/semantic-invariants.md`.

## Browser Validation Logging

- Browser-visible proof is required in SB04 before final closure.
- SB02 must still record component-level UI assertions in `reviews/01-execution-report.md`.

## Progression Gate

- SB04 may start only after failing-first and passing component transcripts prove Manager chat voice eligibility and callbacks are wired.

## Suggested Agent Prompt

```text
Implement this subbundle only.
Work outcome-first: preserve the listed scope boundaries, verify prerequisites before editing, make the smallest correct change set, capture the required proof, update the execution report rows, and stop if the progression gate cannot honestly pass.
```
