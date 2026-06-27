# Manager audio auto-speak parity

## Status

- `Completed`

## Objective

- Make Manager chat automatically synthesize the assistant response after a successful send while Manager voice mode is enabled.

## Success Criteria

- `SendManagerChatMessageAsync` calls the existing `SpeakManagerChatTextAsync` path after receiving non-empty assistant content and `managerChatVoiceModeEnabled` is true.
- Voice access restrictions are preserved.
- A component test proves the assistant response is sent to `IAgentVoiceService.SynthesizeChunksAsync`.

## Covered Inputs

- R001, R002, N001, N002, N003.

## Prerequisites

- Prepared bundle validation passes.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessWorkspaceShell.razor`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\ProcessWorkspaceShellTests.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Voice\VoiceContracts.cs`

## Deliverables

- Auto-speak parity in Manager chat send flow.
- Test fake voice service for Manager component tests.
- Component assertion for synthesis and audio queue calls.

## Dependency Impact

- Browser closure depends on this because the user-visible voice-mode regression is not otherwise observable from static prompt tests.

## Validation Depth

- UI behavior and component-test proof.

## Implementation Steps

1. Add a component test that enables Manager voice mode, sends a prompt, and verifies synthesis of the assistant response.
2. Patch `SendManagerChatMessageAsync` to call `SpeakManagerChatTextAsync` after a successful response when voice mode is enabled.
3. Run the targeted component test.

## Scope Exceptions

- Real microphone capture is covered by existing transcription path and browser availability; this phase proves the post-response speech call.

## Do Not Do

- Do not rewrite voice drivers.
- Do not change agent voice access metadata semantics.

## Acceptance Checklist

- Voice-enabled Manager send synthesizes assistant response.
- Voice-disabled Manager controls remain disabled.
- Manual read path still compiles and existing tests pass.

## Proof Required

- Targeted `dotnet test` component command.
- Execution report row updated.

## Browser Validation Logging

- Deferred to subbundle 03 because this phase is behavior wiring with component proof.

## Progression Gate

- Component test for auto-speak passes.

## Suggested Agent Prompt

```text
Implement this subbundle only.
Work outcome-first: preserve the listed scope boundaries, verify prerequisites before editing, make the smallest correct change set, capture the required proof, update the execution report rows, and stop if the progression gate cannot honestly pass.
```
