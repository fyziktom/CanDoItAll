# SB07 — Reusable Simple Chat components

## Status

- Prepared
- Stage: components
- Proof tier: Behavioral

## Objective

Move reusable Simple Chat presentation/gateway behavior into a MAF Components library, remove route/navigation ownership, and preserve conversation, definition, authorization, and floating-shell behavior.

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
- repo://src/UI/CanDoItAll.Conversations.Components/
- repo://src/UI/CanDoItAll.Conversations.Shell/

## Explicit Non-Goals

- Do not add @page or shell navigation.
- Do not reference Runtime, Persistence, Web, or Agent module.
- Do not change route placement yet.
- Do not replace existing BaseLib/conversation components with raw HTML wrappers.
- Do not add mobile/tablet work.

## Implementation Steps

1. Retry Components MCP libraries/recommendation for workspace, inner tabs, dialogs, scope-neutral states, and floating shell; record exact result.
2. Create SimpleChats.Components with Core/Application and existing UI library references only.
3. Extract a reusable workspace body containing inner Conversations/Definitions tabs without PageScaffold/PageHeader.
4. Move gateways, authorization boundary, presentation models/mappers, follower/reducer, catalog/editor/workspace, and floating shell contributor/content.
5. Split the 788-line workspace controller only along cohesive lifecycle/state/event boundaries with direct tests; do not create partial files.
6. Preserve authorization fail-closed mapping and direct Application calls; no loopback HTTP/SSE.
7. Preserve streaming transient/canonical separation, cancellation/reconnect/recovery, archive/rename/editor and floating-window lifecycle.
8. Remove routed page/navigation contribution from Components.
9. Add registration cardinality, CSS isolation/import/assembly discovery, accessibility, and 1600x1000 component layout tests.
10. Record old UI owner shrink and direct component owner proof.

## Acceptance Criteria

- [ ] Components renders with gateway fakes and no Runtime/Persistence/Agent/Web host.
- [ ] Inner Conversations/Definitions behavior is unchanged.
- [ ] Floating Simple Chat contributor behavior is unchanged and registers once.
- [ ] Components owns no route/navigation.
- [ ] Controller responsibility shrinks without partial growth.
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

Add exact cases WorkspaceBodyHasNoPageScaffold, ComponentsHasNoRouteOrNavigation, FloatingContributorRegistersOnce, ComponentsHasNoRuntimeOrPersistenceReference.

Expected discovery: non-zero for every selector and all four exact new cases.

## Invalidation And Broad-Gate Decision

Stable/Playwright forbidden here. Browser activation waits for SB08. Reopen on component parameters, gateway/auth mapping, shell key/action, streaming presentation, CSS/imports, or project reference.

## UI Composition Contract

Use existing BaseLib/Conversations components.

- Primary surface: selected conversation transcript/composer or definition catalog, depending on the inner tab.
- Supporting content: conversation list, filters, status/action row, and inner tab selector.
- Stats treatment: no new decorative metrics; counts stay compact beside their owning list/tab.
- List/editor organization: browse definitions/conversations in the primary collection; independent definition create/edit remains a Wide dialog so list context returns unchanged.
- Textarea/dialog rationale: system prompt/long description use Extended or explicit domain-sized rows; Wide dense-chrome editor supports provider/settings columns and the body owns scrolling.
- First viewport: selected transcript/composer or usable definition list is visible before page scroll at 1600x1000.
- Scroll owner: workspace list/transcript has one deliberate owner; dialog body owns dialog scrolling.
- Container proof: inner tabs, action/filter rows, and form groups are tested in representative narrow grid/card/rail/dialog columns even on a wide page.
- No routed page chrome exists in the reusable body.

## C# Architecture Impact

Moves reusable Razor/application presentation behavior from a Modules assembly into a MAF feature Components assembly.

## Boundary Ownership

Components owns reusable presentation. Modules.AgentFramework later owns route/tab/navigation composition.

## Dependency Direction

Components -> Application -> Core and neutral UI libraries; never -> Runtime/Persistence/Agent module/Web.

## Pattern Decision

Presentation gateway/adapters and reusable component composition. Extract controller collaborators only where lifecycle responsibility is independently testable.

## Testability Contract

All component behavior runs from fakes in the Components workspace; browser proof supplements rather than replaces this.

## Partial Class Policy

No new partial. Razor-generated partial is not permission to split unrelated state across code-behind fragments.

## Architecture Proof Required

Before/after project graph, source ownership, direct component tests, forbidden-reference/route/navigation negatives, controller shrink, no cycle/partial, review gate.

## Progression Gate

- SB07 plus CP2 Pass unlocks SB08.

## Reopen Triggers

- route/navigation leaks into Components;
- Runtime/Persistence reference;
- duplicated shell registration;
- auth/streaming/floating regression;
- MCP recommendation changes chosen control contract.

## Covered Inputs

- Raw request: isolate reusable Simple Chat components/helpers rather than placing all classes in Agent module.
- Requirements ASCC-003, ASCC-008, ASCC-012, ASCC-014, ASCC-016, ASCC-032, ASCC-040–043, ASCC-046.

## Exact Source References

- repo://src/Modules/CanDoItAll.Modules.LlmChats.Ui/LlmChatConversationWorkspace.razor
- repo://src/Modules/CanDoItAll.Modules.LlmChats.Ui/LlmChatConversationWorkspaceController.cs
- repo://src/Modules/CanDoItAll.Modules.LlmChats.Ui/LlmChatDefinitionEditorDialog.razor
- repo://src/Modules/CanDoItAll.Modules.LlmChats.Ui/LlmChatConversationShellContributor.cs

## Deliverables

- MAF SimpleChats.Components workspace/editor/gateways/floating contributor with no route/navigation/runtime/persistence dependency.

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
- Screenshots: SB07-workspace-normal, SB07-definition-editor-open, SB07-floating-simple-chat-open.
- Review: first useful workspace content, one scroll owner, dialog-body scroll, compound controls in actual narrow containers, overlay/clipping/focus, zero unhandled console/page errors.
