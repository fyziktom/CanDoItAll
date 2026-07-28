# CanDoItAll.AgentFramework.Voice

## Purpose

Provider-backed voice services for agent workflows: browser audio transcription, text-to-speech synthesis, speech text preprocessing, chunked synthesis, voice access checks, and confirmation intent classification.

## Project Type

- SDK: `Microsoft.NET.Sdk`
- Target framework(s): `net10.0`
- Validation command:

```powershell
dotnet build src/MAF/Common/CanDoItAll.AgentFramework.Voice/CanDoItAll.AgentFramework.Voice.csproj
```

## Dependencies

The authoritative project and package dependency list is in [CanDoItAll.AgentFramework.Voice.csproj](CanDoItAll.AgentFramework.Voice.csproj). This README focuses on the project's purpose, boundaries, and validation.

## Architecture Notes

Keep provider-specific speech implementations behind `IAgentVoiceService`, `IAgentVoiceDriverFactory`, `ISpeechToTextVoiceDriver`, and `ITextToSpeechVoiceDriver`. UI modules should pass typed `AgentVoiceTranscriptionRequest` and `AgentVoiceSynthesisRequest` objects instead of calling OpenAI voice endpoints directly.

Voice settings are normalized through the AgentFramework workflow settings path. Do not add fallback voice behavior that hides provider errors; voice callers need explicit errors so UI and process automation can surface actionable state.

## Related Docs

- Repository overview: `README.md` at the repo root
- Current architecture: `docs/architecture-beta.md`
