# SB03 Semantic Invariants

## Invariant SB03-PROVIDER-RUNTIME-STT-TTS

- Invariant ID: `SB03-PROVIDER-RUNTIME-STT-TTS`
- Source raw note: "we did large refactor of the providers including voice drivers. analyze where we have troubles and drivers are not connected well."
- Expected behavior: `AgentVoiceService` speech-to-text and text-to-speech requests dispatch through `AgentVoiceDriverFactory` into `ProviderRuntimeVoiceDriver`, then into typed provider interfaces for the configured provider profile.
- Disallowed shallow implementation: Leave speech-to-text covered only by a static fake voice driver, or let UI code call provider-specific OpenAI endpoints directly.
- Failing-first test: N/A, explicit process/non-production exemption; SB03 adds missing provider-runtime coverage rather than changing production behavior.
- Passing test: `bundle://proof/SB03/transcripts/provider-voice-runtime-tests.txt`.
- Changed source files: `repo://tests/CanDoItAll.Tests.Unit/AgentVoiceTests.cs` SHA-256 `F8C2AC81D3150C88E3ECD2E40E05FC1CA136322C71A5E66B9E0AC5909F2A9A51`.
- Production assertions: `repo://src/CanDoItAll.AgentFramework.Voice/AgentVoiceService.cs` creates typed speech drivers; `repo://src/CanDoItAll.AgentFramework.Voice/AgentVoiceDriverFactory.cs` returns `ProviderRuntimeVoiceDriver`; `repo://src/CanDoItAll.AgentFramework.Voice/ProviderRuntimeVoiceDriver.cs` dispatches with `AgentProviderCapabilityKind.SpeechToText` and `AgentProviderCapabilityKind.TextToSpeech`.
- Red-team negative case: `AgentVoiceService_Synthesize_UnsupportedProviderCapabilityFailsExplicitly` rejects an unsupported provider capability instead of silently falling back.
- Downstream dependency check: SB04 browser proof can rely on enabled Manager chat controls because SB03 proves the shared provider runtime path is connected.
