# Current State

- `ProcessWorkspaceShell.razor` Manager chat `SendManagerChatMessageAsync` sends the prompt and reloads the workspace, but does not call `SpeakManagerChatTextAsync` after a successful response. Other chat surfaces already do this when voice mode is enabled.
- `StopManagerChatRecordingAndSendAsync` transcribes audio into `managerChatDraftPrompt`, sets voice status to `Sending`, then calls `SendManagerChatMessageAsync`. Because send does not auto-speak, the voice-originated flow stops at text response.
- `SpeakLatestManagerChatAssistantMessageAsync` and `SpeakManagerChatTextAsync` work for the manual read button, matching the user's observation that clicking read succeeds.
- `CreateRuntimeLoadOptions(ProcessWorkspaceDetailTabKey.ManagerChat)` currently sets `IncludeUsageTelemetry = false`. That prevents `CurrentShell.Runtime.Stats` from carrying cost/token data into the Manager tab.
- `BuildManagerChatPrompt` lists selected run, status, attention summary, history window, and loaded runs. It does not include runtime usage stats.
- `ProcessRuntimeWorkspaceProjection.Stats` already has `InputTokens`, `CachedInputTokens`, `OutputTokens`, `TotalTokens`, `EstimatedCost`, and `ActualCost`.
- `ProcessManagerChatPromptClassifier.ShouldDisableRuntimeTools` currently treats `cost` and `tokens` prompts as telemetry-only and disables runtime/workspace tools unless the prompt also mentions artifacts. That is too broad for the user's natural cost/token question.
