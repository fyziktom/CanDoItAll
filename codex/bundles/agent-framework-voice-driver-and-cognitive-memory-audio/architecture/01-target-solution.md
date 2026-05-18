# Target Solution

## Project Boundary

- Add `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Voice\CanDoItAll.AgentFramework.Voice.csproj`.
- The project owns voice driver contracts, factory, OpenAI driver implementation, request/result records, settings normalization, and the application service used by UI surfaces.
- AgentFramework Models own serializable voice metadata and settings records when they must be shared by UI, persistence, and services.

## Driver Shape

```mermaid
flowchart LR
    UI["Blazor chat/probe UI"] --> Service["IAgentVoiceService"]
    Service --> Settings["IAgentVoiceSettingsService"]
    Service --> Factory["IAgentVoiceDriverFactory"]
    Factory --> OpenAI["OpenAI voice driver"]
    Factory -. later .-> Local["Local voice driver"]
    OpenAI --> Creds["IAgentProviderCredentialResolver"]
    Creds --> Provider["Provider profile / secret / environment"]
```

- TTS and STT are separate capabilities even when the same driver/provider handles both.
- Driver resolution is exact: disabled, missing provider, unsupported driver, or failed credential resolution produces a surfaced error.
- OpenAI provider code calls official REST endpoints and keeps request construction testable behind `HttpClient`.

## Settings

- General voice settings include:
  - STT enabled/driver/provider/model/language.
  - TTS enabled/driver/provider/model/voice/response format/sample text.
  - A visible AI-generated audio disclosure string.
- Per-agent voice settings include:
  - `CanUseVoiceMode`.
  - optional `PreferredVoiceId`.
- Effective TTS voice resolution order:
  1. per-agent `PreferredVoiceId` when voice mode is allowed and the value is configured
  2. general TTS voice
  3. no fallback; validation error if neither is configured for enabled TTS

## UI Flow

- General voice settings live in the AgentFramework module settings area.
- Agent details gain a Voice tab/section that controls per-agent voice access and override.
- `ChatWorkspacePanel` gains reusable audio-mode controls and visual state parameters.
- Normal chat and contextual floating chat own the JS recording/playback calls and call the shared voice service.

## Cognitive Memory Flow

```mermaid
sequenceDiagram
    participant User
    participant UI as Probe workbench
    participant Voice as IAgentVoiceService
    participant Probe as ICognitiveMemoryProbeService
    participant Review as Review-gated memory path

    User->>UI: Speaks question or correction
    UI->>Voice: Transcribe audio
    UI->>Voice: Speak "wait while I process this"
    UI->>Probe: Ask or prepare AddCorrection feedback
    Probe-->>UI: Answer or interpreted correction
    UI->>Voice: Speak answer/interpretation
    User->>UI: Says yes/ok/store or cancel
    UI->>Voice: Transcribe confirmation
    UI->>Probe: Record feedback only on clear confirmation
    Probe->>Review: Create review-gated repair candidate where applicable
```

- The "store" path is an interaction wrapper over existing probe feedback/review mechanics, not a direct memory mutation API.
