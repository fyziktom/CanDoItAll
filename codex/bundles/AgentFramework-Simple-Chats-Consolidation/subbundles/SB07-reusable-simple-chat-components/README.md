# SB07 — Reusable Simple Chat components

## Status

- Completed — focused proof Pass
- Stage: components
- Proof tier: Behavioral

## Objective

Move reusable Simple Chat presentation/gateway behavior into a MAF Components library, extract one shared Agent/Simple Chat avatar selector, refactor the definition dialog into typed internal settings tabs, remove route/navigation ownership, and preserve conversation, definition, authorization, and floating-shell behavior.

## Owned Requirements

- ASCC-003
- ASCC-008
- ASCC-012
- ASCC-014
- ASCC-016
- ASCC-032
- ASCC-040
- ASCC-041
- ASCC-042
- ASCC-043
- ASCC-046
- ASCC-049
- ASCC-050
- ASCC-051
- ASCC-052

## Prerequisites

- SB05
- CP1 Pass

## Current Source Anchors

- repo://src/Modules/CanDoItAll.Modules.LlmChats.Ui/LlmChatConversationWorkspace.razor
- repo://src/Modules/CanDoItAll.Modules.LlmChats.Ui/LlmChatConversationWorkspaceController.cs
- repo://src/Modules/CanDoItAll.Modules.LlmChats.Ui/LlmChatDefinitionCatalogPanel.razor
- repo://src/Modules/CanDoItAll.Modules.LlmChats.Ui/LlmChatDefinitionEditorDialog.razor
- repo://src/Modules/CanDoItAll.Modules.LlmChats.Ui/LlmChat*UiGateway.cs
- repo://src/Modules/CanDoItAll.Modules.LlmChats.Ui/LlmChatConversationShellContributor.cs
- repo://src/Modules/CanDoItAll.Modules.AgentFramework/Pages/Components/AgentDetailsDialog.razor
- repo://src/Modules/CanDoItAll.Modules.AgentFramework/Pages/Components/AgentDetailsDialog.razor.cs
- repo://src/Modules/CanDoItAll.Modules.AgentFramework/Pages/Components/AgentAvatarUploadFormatter.cs
- repo://src/Modules/CanDoItAll.Modules.AgentFramework/Services/AgentAvatarGenerationService.cs
- repo://src/MAF/Common/CanDoItAll.AgentFramework.Components/
- repo://src/UI/CanDoItAll.Conversations.Components/
- repo://src/UI/CanDoItAll.Conversations.Shell/

## Explicit Non-Goals

- Do not add @page or shell navigation.
- Do not reference Runtime, Persistence, Web, or Agent module.
- Do not change route placement yet.
- Do not replace existing BaseLib/conversation components with raw HTML wrappers.
- Do not copy the Agent avatar selector into SimpleChats.Components or move provider selection/persistence into the shared selector.
- Do not add mobile/tablet work.

## Implementation Steps

1. Retry Components MCP libraries/recommendation for workspace, ModalCompact Tabs, Wide dialog, Avatar, upload, explicit feedback states, and floating shell; record exact result. If transport remains unavailable, use the locally verified AgentDetailsDialog BaseLib composition and record the gap.
2. Create SimpleChats.Components with Core/Application, AgentFramework.Components, and existing neutral UI library references only.
3. Extract a reusable workspace body containing inner Conversations/Definitions tabs without PageScaffold/PageHeader.
4. Move gateways, authorization boundary, presentation models/mappers, follower/reducer, catalog/editor/workspace, and floating shell contributor/content.
5. Extract the inline AgentDetailsDialog avatar selector into one reusable AgentFramework.Components component. It owns preview/catalog/default-reset/upload presentation and a typed AI-generation callback, while host editors own provider resolution and persistence.
6. Replace AgentDetailsDialog's inline avatar markup/state with the shared selector and preserve all existing Agent avatar behaviors/tests.
7. Replace the Simple Chat raw avatar URL field with the same selector and add a narrow ILlmChatAvatarUiGateway for generation availability and execution; keep the implementation outside SimpleChats.Components.
8. Refactor LlmChatDefinitionEditorDialog into typed Identity, Runtime, and Output-and-revision ModalCompact tabs with validation above panels and stable footer actions.
9. Split the 788-line workspace controller only along cohesive lifecycle/state/event boundaries with direct tests; do not create partial files.
10. Preserve authorization fail-closed mapping and direct Application calls; no loopback HTTP/SSE.
11. Preserve streaming transient/canonical separation, cancellation/reconnect/recovery, archive/rename/editor and floating-window lifecycle.
12. Remove routed page/navigation contribution from Components.
13. Add registration cardinality, CSS isolation/import/assembly discovery, accessibility, settings-tab, shared-avatar, save/cancel, and 1600x1000 component layout tests.
14. Record AgentDetailsDialog and old UI owner shrink plus direct component owner proof.

## Acceptance Criteria

- [ ] Components renders with gateway fakes and no Runtime/Persistence/Agent/Web host.
- [ ] Inner Conversations/Definitions behavior is unchanged.
- [ ] Floating Simple Chat contributor behavior is unchanged and registers once.
- [ ] Components owns no route/navigation.
- [ ] Controller responsibility shrinks without partial growth.
- [ ] Definition editor uses Identity, Runtime, and Output and revision tabs; validation and Cancel/Save remain visible.
- [ ] Agent and Simple Chat editors use the same shared avatar selector implementation.
- [ ] Simple Chat has no raw avatar URL input and supports preview, bundled choice, default reset, validated upload, AI success/unavailable/error, cancel, and save-only persistence.
- [ ] Agent avatar generation/upload behavior remains unchanged after extraction.
- [ ] Component/accessibility tests pass.

## Validation Depth

- Proof tier: Behavioral.
- Critical foundation: yes for SB08 route/tab/floating composition.

Behavioral plus architecture evidence: component failing-first/characterization proof, rendering snapshots, registration/source guards, direct owner tests, and MCP composition record.

## Focused Test Selection

Workspaces:

- tests/Solutions/CanDoItAll.Tests.Components.slnx
- tests/Solutions/CanDoItAll.Tests.Unit.slnx

Required:

- LlmChatConversationWorkspaceTests
- LlmChatDefinitionUiTests
- LlmChatConversationShellContributorTests
- LlmChatUiCompositionTests
- LlmChatUiBoundaryTests
- ConversationShellHostTests
- AgentDetailsDialogAvatarGenerationTests
- AgentAvatarSelectorTests

Add exact cases WorkspaceBodyHasNoPageScaffold, ComponentsHasNoRouteOrNavigation, FloatingContributorRegistersOnce, ComponentsHasNoRuntimeOrPersistenceReference, DefinitionEditorUsesTypedSettingsTabs, DefinitionEditorUsesSharedAvatarSelector, SharedAvatarSelectorHandlesBundledResetAndUpload, SharedAvatarSelectorHandlesAiSuccessUnavailableAndError, CancellingDefinitionDiscardsAvatar, SavingDefinitionPersistsAvatar, and AgentEditorUsesSharedAvatarSelector.

Expected discovery: non-zero for every selector and all four exact new cases.

## Invalidation And Broad-Gate Decision

Stable/Playwright forbidden here. Browser activation waits for SB08. Reopen on component parameters, gateway/auth mapping, shell key/action, streaming presentation, CSS/imports, or project reference.

## UI Composition Contract

Use existing BaseLib/Conversations components.

- Primary surface: selected conversation transcript/composer or definition catalog, depending on the inner tab.
- Supporting content: conversation list, filters, status/action row, and inner tab selector.
- Stats treatment: no new decorative metrics; counts stay compact beside their owning list/tab.
- List/editor organization: browse definitions/conversations in the primary collection; independent definition create/edit remains a Wide dialog so list context returns unchanged. Internal tabs are Identity, Runtime, and Output and revision.
- Textarea/dialog rationale: system prompt/long description use Extended or explicit domain-sized rows; Wide dense-chrome editor supports the tabbed configuration surface and the body owns scrolling while validation/header/footer stay stable.
- Avatar organization: ConversationIdentityFields renders preview/actions; Choose avatar opens the shared Wide selector with bundled grid, upload, and AI generation. The editor stages ValueChanged and persists only through its existing Save mutation.
- First viewport: selected transcript/composer or usable definition list is visible before page scroll at 1600x1000.
- Scroll owner: workspace list/transcript has one deliberate owner; dialog body owns dialog scrolling.
- Container proof: inner tabs, action/filter rows, and form groups are tested in representative narrow grid/card/rail/dialog columns even on a wide page.
- No routed page chrome exists in the reusable body.

## C# Architecture Impact

Moves reusable Razor/application presentation behavior from a Modules assembly into a MAF feature Components assembly.

## Boundary Ownership

Components owns reusable presentation. Modules.AgentFramework later owns route/tab/navigation composition.

## Dependency Direction

SimpleChats.Components -> Application -> Core plus AgentFramework.Components and neutral UI libraries; never -> Runtime/Persistence/Agent module/Web. AgentFramework.Components never references SimpleChats or Modules.

## Pattern Decision

Presentation gateway/adapters and reusable component composition. The shared avatar selector uses a typed callback/gateway adapter so provider execution does not leak into either UI library. Extract controller collaborators only where lifecycle responsibility is independently testable.

## Testability Contract

All component behavior runs from fakes in the Components workspace; browser proof supplements rather than replaces this.

## Partial Class Policy

No new partial. Razor-generated partial is not permission to split unrelated state across code-behind fragments.

## Architecture Proof Required

Before/after project graph, source ownership, direct component tests, forbidden-reference/route/navigation negatives, one avatar-selector source guard, AgentDetailsDialog/controller shrink, no cycle/partial, review gate.

## Progression Gate

- SB07 plus CP2 Pass unlocks SB08.

## Reopen Triggers

- route/navigation leaks into Components;
- Runtime/Persistence reference;
- duplicated shell registration;
- auth/streaming/floating regression;
- duplicated avatar implementation, lost Agent avatar behavior, raw Simple Chat avatar URL field, hidden-tab validation, or gateway/provider-runtime leak;
- MCP recommendation changes chosen control contract.

## Covered Inputs

- Raw requests: isolate reusable Simple Chat components/helpers rather than placing all classes in Agent module; make the Simple Chat settings dialog tabbed like Agent settings; reuse the complete Agent avatar selector including AI generation.
- Requirements ASCC-003, ASCC-008, ASCC-012, ASCC-014, ASCC-016, ASCC-032, ASCC-040–043, ASCC-046, ASCC-049–052.

## Exact Source References

- repo://src/Modules/CanDoItAll.Modules.LlmChats.Ui/LlmChatConversationWorkspace.razor
- repo://src/Modules/CanDoItAll.Modules.LlmChats.Ui/LlmChatConversationWorkspaceController.cs
- repo://src/Modules/CanDoItAll.Modules.LlmChats.Ui/LlmChatDefinitionEditorDialog.razor
- repo://src/Modules/CanDoItAll.Modules.LlmChats.Ui/LlmChatConversationShellContributor.cs
- repo://src/Modules/CanDoItAll.Modules.AgentFramework/Pages/Components/AgentDetailsDialog.razor
- repo://src/Modules/CanDoItAll.Modules.AgentFramework/Pages/Components/AgentDetailsDialog.razor.cs

## Deliverables

- MAF SimpleChats.Components workspace/editor/gateways/floating contributor with no route/navigation/runtime/persistence dependency, typed definition settings tabs, a Simple Chat avatar gateway contract, and one shared AgentFramework avatar selector consumed by both editors.

## Dependency Impact

- SB08/SB11 use these components; route/floating/browser proof is invalid if gateway/auth/streaming behavior or registration changes later.

## Acceptance Checklist

- All Acceptance Criteria above pass, including direct fake-driven tests, no route/navigation, one shell contributor, and controller shrink.

## Proof Required

- proof/SB07 execution report with component commands/results, realistic positive/negative render evidence, source/reference/registration guards, and reviewed normal/editor/floating screenshots.

## Browser Validation Logging

- Route: current /chats characterization host before SB08 cutover, or an equivalent representative component host.
- Viewport: 1600x1000 only; BaseLib itself is not modified.
- Actions: open Conversations/Definitions, select/create/edit, open editor, send/cancel/reconnect where deterministic, open floating Simple Chat.
- Screenshots: SB07-workspace-normal, SB07-definition-identity-tab, SB07-definition-runtime-tab, SB07-definition-output-revision-tab, SB07-simple-chat-avatar-selector-open, SB07-agent-avatar-selector-open, SB07-avatar-ai-unavailable-or-success, SB07-floating-simple-chat-open.
- Review: first useful workspace content, all internal tabs/selected semantics, shared selector parity, one scroll owner, dialog-body scroll, stable footer actions, compound controls in actual narrow containers, overlay/clipping/focus, zero unhandled console/page errors.
