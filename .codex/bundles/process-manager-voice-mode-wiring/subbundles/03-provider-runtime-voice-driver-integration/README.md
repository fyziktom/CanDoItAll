# Provider runtime voice driver integration

## Status

- `Completed`

## Objective

- Prove the refactored provider runtime voice drivers are connected for speech-to-text and text-to-speech and still fail explicitly for unsupported voice capabilities.

## Success Criteria

- STT dispatch from `AgentVoiceService` reaches an `IProviderSpeechToTextDriver`.
- TTS dispatch from `AgentVoiceService` reaches an `IProviderTextToSpeechDriver`.
- Unsupported capability or wrong provider purpose still raises an explicit provider capability/configuration error.
- Existing OpenAI voice request-shape tests pass.

## Covered Inputs

- N002, N005.
- R004.

## Prerequisites

- SB01 closure gate passed.

## Exact Source References

- `repo://src/CanDoItAll.AgentFramework.Voice/AgentVoiceService.cs`
- `repo://src/CanDoItAll.AgentFramework.Voice/AgentVoiceDriverFactory.cs`
- `repo://src/CanDoItAll.AgentFramework.Voice/ProviderRuntimeVoiceDriver.cs`
- `repo://src/CanDoItAll.AgentFramework.Providers/Contracts/ProviderCapabilityContracts.cs`
- `repo://src/CanDoItAll.AgentFramework.Providers/Drivers/OpenAiProviderDriver.cs`
- `repo://tests/CanDoItAll.Tests.Unit/AgentVoiceTests.cs`
- `repo://tests/CanDoItAll.Tests.Unit/AgentFramework/Providers/ConcreteProviderDriverTests.cs`
- `repo://tests/CanDoItAll.Tests.Unit/AgentFramework/Providers/ProviderArchitectureFoundationTests.cs`

## Deliverables

- Test proof that STT and TTS provider runtime dispatch are connected.
- Any minimal provider-driver repair if tests expose a real defect.
- `bundle://proof/SB03/manifest.md` and `bundle://proof/SB03/semantic-invariants.md`.

## Dependency Impact

- SB04 final browser proof depends on this phase so enabled controls are not hiding a broken provider runtime.
- If this phase finds a provider driver defect, SB02 browser proof must be rerun after repair.

## Validation Depth

- Critical provider foundation with semantic positive STT/TTS proof and adversarial unsupported-provider proof.

## Implementation Steps

1. Run targeted existing `AgentVoiceTests` and provider driver tests.
2. Add missing STT runtime dispatch coverage if existing tests cover only TTS.
3. Verify unsupported provider/capability failure remains explicit.
4. Make a minimal provider repair only if a test proves a real broken connection.
5. Capture transcripts and proof manifest.

## Scope Exceptions

- This phase does not require live external OpenAI calls; fake provider drivers can prove typed runtime wiring.

## Do Not Do

- Do not introduce fallback provider selection.
- Do not let image-generation or chat-only provider profiles satisfy voice capability.
- Do not move provider-specific request JSON into UI code.

## Acceptance Checklist

- STT and TTS runtime dispatch tests pass.
- Unsupported capability test passes.
- OpenAI request-shape tests pass or remain covered by existing passing tests.
- Source assertions show `AgentVoiceDriverFactory` returns provider runtime voice driver for OpenAI voice driver kind.

## Proof Required

- `bundle://proof/SB03/transcripts/provider-voice-runtime-tests.txt`
- `bundle://proof/SB03/transcripts/source-assertions.txt`
- `bundle://proof/SB03/transcripts/anti-stub-audit.txt`
- `bundle://proof/SB03/manifest.md`
- `bundle://proof/SB03/semantic-invariants.md`

## Browser Validation Logging

- N/A. Provider runtime proof is test/source based; browser integration is SB04.

## Progression Gate

- SB04 may close only after STT, TTS, and unsupported-capability provider transcripts pass.

## Suggested Agent Prompt

```text
Implement this subbundle only.
Work outcome-first: preserve the listed scope boundaries, verify prerequisites before editing, make the smallest correct change set, capture the required proof, update the execution report rows, and stop if the progression gate cannot honestly pass.
```
