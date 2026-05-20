# Target Solution

## Target Architecture

Add a module-owned `ICognitiveMemoryCuratorConversationService` that provides a single contract for:

- opening/reusing curator sessions
- sending a user turn in either `Agent` or `DirectLlm` mode
- recalling Cognitive Memory context before answer generation
- storing the curator answer with the recall trace and included memory ids
- extracting user correction/new-knowledge signals from the turn
- creating trusted source/evidence/mutation/consolidation artifacts with high priority and confidence

The UI calls only this curator service and the existing voice service. It must not construct memory-source or mutation records directly.

## Runtime Modes

- `Agent`: Use the configured/default Cognitive Memory agent through `IAgentFrameworkWorkspaceService.SendMessageAsync` or an execution-run path that can suppress approvals for trusted curator runs. The shared curator service still performs recall before the agent turn so correction targeting has a trace.
- `DirectLlm`: Use the configured/default provider through `IAgentFrameworkWorkspaceService.RunProviderTestChatAsync` with a curator system prompt and the recalled context pack rendered into the messages.

Both modes return a `CognitiveMemoryCuratorTurnResult` with response text, mode, recall trace id, context pack id, included memory ids, captured improvements, warnings, and provider/agent metadata.

## Memory Improvement Path

Captured human input must create:

- a source manifest with source system `CuratorConversation`
- a source item for the user utterance/correction
- an evidence anchor with trust level `HumanReview` and direction appropriate to the correction/new fact
- a mutation command with `RequiresHumanReview = false`
- a consolidation candidate or direct accepted artifact that downstream dreaming can cluster and aggregate

Corrections must include the preceding curator turn id, recall trace id, and included memory record ids. New knowledge can omit affected memory ids but must still include actor credit and confidence metadata.

## UI Strategy

Add a `Curator` tab to `/cognitive-memory` with:

- mode segmented/select control for `Agent` vs `Direct LLM`
- active session and current project facts
- transcript list
- text composer and send button
- audio mode, record, and speak controls using existing voice JS
- captured improvement list for the latest turn
- concise status/error badges

Use existing components (`PageScaffold`, `Tabs`, `Grid`, `Stack`, `Cluster`, `SurfaceCard`, `StatusBadge`, `Button`, `TextBlock`) and page CSS only for layout gaps that the component parameters cannot express.
