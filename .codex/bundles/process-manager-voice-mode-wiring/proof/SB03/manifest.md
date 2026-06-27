# SB03 Proof Manifest

- Subbundle: `SB03 Provider runtime voice driver integration`
- Status: `Completed`
- Owned requirements: R004
- Owned raw notes: N002, N005
- Semantic invariant contract: `bundle://proof/SB03/semantic-invariants.md`

## Changed File Manifest

| File | Before SHA-256 | After SHA-256 | Notes |
| --- | --- | --- | --- |
| `repo://tests/CanDoItAll.Tests.Unit/AgentVoiceTests.cs` | `pre-existing dirty worktree before SB03; exact pre-SB03 hash not captured` | `F8C2AC81D3150C88E3ECD2E40E05FC1CA136322C71A5E66B9E0AC5909F2A9A51` | Added service-level STT provider-runtime dispatch coverage. |
| `bundle://proof/SB03/transcripts/provider-voice-runtime-tests.txt` | `new` | `787BC3319D15569163D21EB2EF42BDA24B4A9E7697C9E374391BC4F93E4D9DF4` | Passing provider voice runtime transcript. |
| `bundle://proof/SB03/transcripts/source-assertions.txt` | `new` | `886B2674B175AD6CAC9E720206929641599F4FFD4DE91A011D27FDAEA4E23717` | Source assertions. |
| `bundle://proof/SB03/transcripts/anti-stub-audit.txt` | `new` | `8BB39E6A499ADC4B8B960CA531F62F6AAB3C5DD0C70CABA3010C5499258E8A6A` | Anti-stub audit. |

## Command Transcripts

- Failing-first: N/A, process/non-production coverage extension; no production behavior changed.
- Passing transcript: `bundle://proof/SB03/transcripts/provider-voice-runtime-tests.txt`.
- Source assertions: `bundle://proof/SB03/transcripts/source-assertions.txt`.
- Anti-stub audit: `bundle://proof/SB03/transcripts/anti-stub-audit.txt`.

## Semantic Adequacy Evidence

- Raw note owned: N005 "provider/voice-driver refactor trouble"; N002 shared general voice flow.
- Shipped behavior: no production provider-driver code changed; the missing service-to-runtime STT test now proves the refactored runtime path.
- Source proof: `bundle://proof/SB03/transcripts/source-assertions.txt`.
- Test proof: `bundle://proof/SB03/transcripts/provider-voice-runtime-tests.txt`.
- Shallow-pass trap: a static fake driver test would not prove `ProviderRuntimeVoiceDriver` or concrete provider dispatch.
- Adversarial negative proof: `CanDoItAll.Tests.Unit.AgentVoiceTests.AgentVoiceService_Synthesize_UnsupportedProviderCapabilityFailsExplicitly`.
- Semantic positive proof: `CanDoItAll.Tests.Unit.AgentVoiceTests.AgentVoiceService_Transcribe_UsesProviderRuntimeSpeechDriver`.
- Anti-stub audit: `bundle://proof/SB03/transcripts/anti-stub-audit.txt`.

## Proof Claim To Code Matrix

| Capability claim | Required production source proof | Required test proof | Required negative fixture | Result |
| --- | --- | --- | --- |
| STT provider runtime dispatch is connected | `repo://src/CanDoItAll.AgentFramework.Voice/ProviderRuntimeVoiceDriver.cs` in `bundle://proof/SB03/transcripts/source-assertions.txt` | `bundle://proof/SB03/transcripts/provider-voice-runtime-tests.txt` | N/A, process/non-production coverage addition | `Passed` |
| TTS provider runtime dispatch still works | `repo://src/CanDoItAll.AgentFramework.Voice/AgentVoiceService.cs` in `bundle://proof/SB03/transcripts/source-assertions.txt` | `bundle://proof/SB03/transcripts/provider-voice-runtime-tests.txt` | Unsupported capability test | `Passed` |
| Unsupported provider capability fails explicitly | `repo://src/CanDoItAll.AgentFramework.Voice/ProviderRuntimeVoiceDriver.cs` in `bundle://proof/SB03/transcripts/source-assertions.txt` | `bundle://proof/SB03/transcripts/provider-voice-runtime-tests.txt` | `AgentVoiceService_Synthesize_UnsupportedProviderCapabilityFailsExplicitly` | `Passed` |
