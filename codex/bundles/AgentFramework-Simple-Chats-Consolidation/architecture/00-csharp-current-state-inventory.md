# C# current-state inventory

## Snapshot

- Prepared repository head: 30edf7b034cb2a06d29ee3ba2df8193006109dd5
- Fresh scoped CodeAnalytics evidence used for the final preparation review: snap-20260817163454-e036fa6f
- A wider architecture scan reported 16 affected projects, 638 documents, 1,889 types, 15,887 members, and 89 DI registrations.
- No blocking analyzer error and no LlmChats project-level cycle was reported.
- Existing AgentFramework module/type cycles are baseline debt outside the new path. CP0 must record their exact identities, and subsequent gates require no new cycle/no enlargement.

## Current project responsibilities

### CanDoItAll.Modules.LlmChats

Owns:

- strongly typed identifiers and paging/fingerprint helpers;
- definitions and revisions;
- conversation and operation domain state;
- application commands, readers, dispatcher/executor/state machine;
- leases, event sessions, cancellation/recovery policies;
- repository, evidence, provider, and execution ports;
- application DI.

Conclusion: this is not merely a thin module. Domain and Application are separable compile-time owners.

### CanDoItAll.Modules.LlmChats.Persistence

Owns two unrelated outer concerns:

- EF rows/configuration/repositories/read stores, transfer, DB profile/lease implementations;
- provider profile resolution, invocation adapters/decorators, conversation engine, runtime scope construction.

Conclusion: Persistence is the primary accidental boundary and must split into Runtime and Persistence.

### CanDoItAll.Modules.LlmChats.Ui

Owns:

- reusable workspace, editor/catalog, gateways, presentation mappers, controller/follower, authorization facade, floating shell contributor;
- /chats routed PageScaffold, shell navigation contributor, assembly/host registration.

Conclusion: reusable Components must move to MAF; route and navigation belong to the Agent product module.

## Existing reusable seams

- CanDoItAll.AgentFramework.Llm.Abstractions owns provider-neutral invocation and message contracts.
- CanDoItAll.AgentFramework.Llm.Conversations owns reusable conversation service/store behavior.
- CanDoItAll.AgentFramework.Llm.ProviderRuntime owns provider-runtime adaptation.
- CanDoItAll.Conversations.Components and CanDoItAll.Conversations.Shell own source-neutral Razor and floating-shell primitives.
- IProviderRuntimeProfileSource and IProviderModelCapabilityResolver already provide canonical profile/model access.

These seams are retained. The initiative does not create a redundant SimpleChats.Abstractions assembly.

## Current usage/cost split

Agent:

- ProviderUsageObservation includes provider/model, tokens, status, run/agent/session correlations, cost, and pricing hash/version.
- AgentFrameworkWorkspaceExecutionService.Usage enriches/prices observations.
- FileSandboxWorkspaceExecutionSliceStore builds AgentUsageProjection.
- AgentFrameworkWorkspaceService returns Agent-specific overview/detail snapshots.

Simple Chat:

- LlmChatInvocationRecord and LlmChats_InvocationRecords identify attempts by OperationId + Ordinal.
- They store provider/profile/model, three token counts, outcome/failure, time, and correlation.
- They do not store explicit usage completeness, reasoning/cache-write totals, cost, or immutable pricing provenance.
- Transcript messages also copy successful token usage but omit failed/retried attempts.

Conclusion: invocation attempts are the authoritative chat cost source; transcript and terminal operation totals are excluded.

## Current UI

- /agents uses SecondaryTabs with overview, agents, providers, voice, floating-chat, chat, capabilities, governance, diagnostics.
- /chats is a separate PageScaffold with inner Conversations and Definitions tabs.
- A separate Simple Chats navigation contributor points to /chats.
- Agent overview/provider/model/agent usage methods have no workload selection.
- LlmChatDefinitionEditorDialog is one vertically stacked form with no internal settings Tabs. Identity/provider controls are always visible and output/revision fields sit in an advanced section.
- The Simple Chat avatar field is a raw URL TextBox even though the definition already persists AvatarImageUrl and list/chat surfaces render it.
- AgentDetailsDialog already provides current preview, bundled avatars, reset, validated upload, and configured-provider AI generation, but the selector markup/state is embedded in the large dialog instead of a reusable component.
- AgentDetailsDialog.razor.cs is a 1,600+ line partial component with about 174 collected members; adding a second consumer by copying its avatar methods would deepen responsibility concentration.

Follow-up architecture evidence: scoped CodeAnalytics snapshot snap-20260817172927-da2eea1a loaded the five current Agent/AgentFramework.Components/LlmChats projects, 258 documents, no blocking errors, and confirmed AgentDetailsDialog, AgentAvatarGenerationService, and AgentAvatarUploadFormatter remain owned by Modules.AgentFramework. The Components MCP transport was unavailable on both library-list and recommendation calls; SB07 must retry before markup changes and otherwise follow the locally verified BaseLib Tabs/Dialog/Avatar composition already used by AgentDetailsDialog.

## Concentration and partial-class observations

- LlmChatConversationWorkspaceController.cs: about 788 lines.
- LlmChatsTransferDocument.cs: about 517 lines.
- LlmChatConversationShellContributor.cs: about 441 lines.
- LlmChatConversationEngine.cs: about 393 lines.
- LlmChatOperationStateMachine.cs: about 356 lines.
- AgentFrameworkWorkspaceExecutionService.Usage.cs is a partial-class responsibility slice.

Moving bytes without changing owners is insufficient. Critical phases must prove the new owner directly and show the old owner shrinking or disappearing. No new partial class is permitted as an extraction technique.

## Current compatibility surfaces

- HTTP: /api/llm-chats, /api/llm-conversations, /api/llm-chat-operations.
- API authorization scopes/policies for read/manage/execute LlmChats.
- SSE cursor/replay behavior.
- LlmChats_* relational table mappings and historical PostgreSQL migrations.
- database transfer module identity llm-chats.
- /chats browser route and floating-shell source keys.
- AppDbContext configuration scanning and module assembly registration.
