# Current State

## Existing Cognitive Memory Surface

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Pages\CognitiveMemoryPage.razor` owns the `/cognitive-memory` page and already uses tabbed workspace UI.
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Pages\Components\CognitiveMemoryProbeWorkbenchTab.razor` provides a question/feedback probe flow, but it is form-heavy and not a fluent conversation.
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Pages\CognitiveMemoryPage.razor.cs` already injects `IAgentFrameworkWorkspaceService`, `IAgentVoiceService`, and `IJSRuntime`, so no new UI dependency family is needed.

## Existing Memory Capture And Repair

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Advanced\CognitiveMemoryAdvancedServices.cs` records probe turns with recall trace ids and can create source-backed repair candidates for incorrect, wrong-scope, or correction feedback.
- Current probe feedback intentionally sets `RequiresHumanReview = true` and creates pending review items. That conflicts with the trusted curator requirement to skip manual confirmations/approvals.
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Consolidation\CognitiveMemoryConsolidationCandidateApplicator.cs` materializes accepted source-backed candidates into memory records and claims.
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Recall\CognitiveMemoryRecallServices.cs` persists recall traces and selected memory/source refs, which are needed to target corrections at the wrong memory used by the curator answer.

## Existing Agent And Voice Support

- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Components\ChatWorkspacePanel.razor` and `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Pages\Components\AgentChatPanel.razor.cs` already support voice recording, transcription, and spoken assistant responses in agent chat.
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Voice\AgentVoiceService.cs` performs speech-to-text and text-to-speech through configured providers.
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Components\wwwroot\js\agent-framework-voice.js` exposes the browser recording and playback bridge.
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Settings\CognitiveMemorySettingsContracts.cs` already stores default provider/default agent settings and execution profiles for Cognitive Memory roles.

## Gap

The repository has pieces of the solution, but no unified curator conversation service exists that combines recall, LLM/agent response, transcript persistence, automatic correction detection, high-confidence trusted-human capture, and UI/voice in one flow.
