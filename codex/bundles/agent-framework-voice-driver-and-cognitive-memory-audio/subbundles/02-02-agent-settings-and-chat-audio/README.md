# 02 Agent Settings And Chat Audio

## Status

Completed

- Subbundle status: `Completed`

## Objective

Persist and expose general/per-agent voice settings, then add audio-mode controls to normal and floating agent chat using the shared chat panel and voice service.

## Covered Inputs

- Agent module settings for TTS/STT drivers and provider connection.
- Same OpenAI API key may be used for both.
- Voice selection and test samples.
- Per-agent voice mode access and voice override.
- Normal and project-structure floating chat audio mode.

## Prerequisites

- `01-01-voice-driver-core` closure gate passed.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Models\Agents\Access\AgentImageGenerationAccessModels.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Models\Editors\EditorModels.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Core\Catalog\AgentFrameworkWorkspaceCatalogService.Agents.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Components\ChatWorkspacePanel.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Components\wwwroot\js\agent-framework-download.js`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Pages\AgentsHomePage.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Pages\Components\AgentChatPanel.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Pages\Components\AgentChatPanel.razor.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Pages\Components\AgentDetailsDialog.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Components\ContextualAgentWorkspaceWindows.razor`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\ChatWorkspacePanelTests.cs`

## Deliverables

- General voice settings UI with STT/TTS enablement, driver/provider/model fields, voice selection, and sample playback.
- Per-agent Voice section/tab with voice mode access and voice override.
- Agent metadata read/write support for `voiceAccess`.
- Shared chat panel audio-mode controls with clear enabled/recording/transcribing/speaking states.
- Normal chat and floating contextual chat recording, transcription, send, synthesis, and playback orchestration.
- JS asset for browser recording/playback with explicit unsupported-browser errors.

## Dependency Impact

- Cognitive Memory voice dialogue depends on the same settings and browser audio primitives.
- If settings or JS contracts change after this phase, subbundle 03 must be reopened.

## Validation Depth

- Unit/component tests for metadata and rendered controls.
- Browser proof for normal `/agents?tab=chat` and floating contextual/project-structure chat.
- Visual review for button labels/icons, state badges, clipping, and mobile/desktop sizing where affected.

## Implementation Steps

1. Add per-agent `AgentVoiceAccessMetadata` and wire it through `AgentEditorModel` and agent save.
2. Add general voice settings service/UI in the AgentFramework module.
3. Add voice controls parameters to `ChatWorkspacePanel`.
4. Add JS recording/playback asset and load it with the application.
5. Wire normal chat to transcribe recorded audio, send the prompt, synthesize assistant output, and play it.
6. Wire contextual/floating chat to the same flow.
7. Add tests and browser proof.

## Scope Exceptions

- Live OpenAI sample playback depends on a configured provider/key and may be environment-blocked. The UI must show a clear error if unavailable.

## Do Not Do

- Do not fork separate normal/floating chat UIs.
- Do not make voice mode available when the agent denies voice access.
- Do not hide synthesis/transcription failures behind text-only behavior.

## Acceptance Checklist

- [x] General voice settings save and reload.
- [x] Per-agent voice settings save and reload.
- [x] Per-agent voice overrides general voice for TTS.
- [x] Normal chat can turn on audio mode and process voice input/output.
- [x] Floating contextual chat can turn on audio mode and process voice input/output.
- [x] Audio controls remain readable and unclipped in the shared panel.

## Proof Required

- `dotnet test tests\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj --no-restore --filter FullyQualifiedName~Voice`
- `dotnet test tests\CanDoItAll.Tests.Components\CanDoItAll.Tests.Components.csproj --no-restore --filter FullyQualifiedName~ChatWorkspacePanel`
- Browser proof logged in `reviews/01-execution-report.md` with screenshots under `evidence/`.

## Browser Validation Logging

Record route, viewport, actions, assertions, screenshots, and result for:

- `/agents?tab=chat` normal chat audio controls.
- Project Structure page contextual agent list and floating chat audio controls.
- Open audio mode, record unsupported/permission path or fake recording path, and sample playback path where configured.

## Progression Gate

- Do not proceed to subbundle 03 until normal and floating chat share the voice UI/service path and browser proof shows no layout or state regressions.

## Suggested Agent Prompt

Implement persisted AgentFramework voice settings and chat audio mode. Use the shared chat panel, keep voice calls in services/parents, preserve per-agent access policy, and prove both normal and floating chat.
