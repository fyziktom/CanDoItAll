# SB08 — Agent page Simple Chats tab and route consolidation

## Status

- Prepared
- Stage: integration-checkpoint
- Proof tier: Governed

## Objective

Place Simple Chats immediately after Agents in the canonical AgentFramework page, preserve its inner workspace/floating behavior, and replace the separate /chats page/navigation with a typed compatibility redirect.

## Owned Requirements

- ASCC-003
- ASCC-004
- ASCC-012
- ASCC-013
- ASCC-016
- ASCC-031
- ASCC-032
- ASCC-033
- ASCC-034
- ASCC-035
- ASCC-039
- ASCC-040
- ASCC-041
- ASCC-042
- ASCC-043
- ASCC-047

## Prerequisites

- SB06
- SB07
- CP2 Pass

## Current Source Anchors

- repo://src/Modules/CanDoItAll.Modules.AgentFramework/Pages/AgentsHomePage.razor
- repo://src/Modules/CanDoItAll.Modules.AgentFramework/Pages/AgentsHomePage.razor.cs
- repo://src/Modules/CanDoItAll.Modules.AgentFramework/AgentFrameworkAgentsChatContextBuilder.cs
- repo://src/Modules/CanDoItAll.Modules.AgentFramework/Services/AgentFrameworkModuleServiceCollectionExtensions.cs
- repo://src/Modules/CanDoItAll.Modules.LlmChats.Ui/Pages/LlmChatsPage.razor
- repo://src/Modules/CanDoItAll.Modules.LlmChats.Ui/Navigation/LlmChatsShellNavigationContributor.cs

## Explicit Non-Goals

- Do not implement dashboard scope yet.
- Do not render the old routed PageScaffold inside /agents.
- Do not move provider configuration into Simple Chats.
- Do not remove HTTP APIs.
- Do not run Stable/full Playwright.

## Implementation Steps

1. Replace scattered top-level tab strings with a centralized typed Agent page tab catalog/parser while preserving current keys.
2. Add simple-chats immediately after agents in AllowedTabs/SecondaryTabs and context mapping.
3. Render the reusable Simple Chat workspace body; preserve its inner typed Conversations/Definitions state and recognized selection IDs.
4. Keep provider setup in Providers and prove Chat-purpose profiles appear in the definition editor.
5. Add /chats redirect-only component/endpoint in Modules.AgentFramework; preserve recognized inner state and prevent loops.
6. Remove LlmChatsShellNavigationContributor and full routed LlmChatsPage from active composition.
7. Register Components and floating contributor exactly once through Agent product composition.
8. Add component/route/navigation/authorization tests and named Playwright cases.
9. Through Playwright MCP at 1600x1000, prove tab order, inner tabs, definition/conversation main flow, /chats redirect, floating Simple Chat, and existing Agent main/floating parity.
10. Record screenshots, console/page errors, scroll/overlay behavior, and architecture/UI gate.

## Acceptance Criteria

- [ ] Simple Chats immediately follows Agents.
- [ ] Inner Conversations/Definitions work without nested page chrome.
- [ ] /chats redirects and preserves recognized state.
- [ ] One shell navigation/contributor registration exists.
- [ ] Providers remain canonical.
- [ ] Main/floating Agent and Simple Chat parity passes named browser proof.

## Validation Depth

- Proof tier: Governed.
- Critical product checkpoint: yes; placement, route, navigation, and shell composition unlock dashboard/final proof.

Governed UI integration with component tests, named Playwright, Playwright MCP screenshots/console evidence, DI/navigation source guards, semantic invariants, and architecture gate.

## Focused Test Selection

Workspaces:

- tests/Solutions/CanDoItAll.Tests.Components.slnx
- tests/Solutions/CanDoItAll.Tests.Playwright.slnx

Required:

- AgentsHomePageTests
- LlmChatConversationWorkspaceTests
- LlmChatDefinitionUiTests
- LlmChatConversationShellContributorTests
- LlmChatUiCompositionTests
- AgentFrameworkSimpleChatsConsolidationPlaywrightTests.SimpleChatsTabImmediatelyFollowsAgents
- AgentFrameworkSimpleChatsConsolidationPlaywrightTests.ChatsRouteRedirectsAndPreservesRecognizedState
- AgentFrameworkSimpleChatsConsolidationPlaywrightTests.MainAndFloatingAgentAndSimpleChatFlowsRemainOperational

Expected discovery: every exact selector discovers one test; selected component classes are non-zero.

## Invalidation And Broad-Gate Decision

Stable and full Playwright forbidden. Named browser scenarios authorized. Reopen on route/tab/query/context mapping/navigation/shell/provider-editor/component-composition change.

## UI Composition Contract

Follow plan/03-ui-composition-contract.md.

- Primary surface: the selected AgentFramework peer tab; for Simple Chats this is the reusable conversation/definition workspace.
- Supporting content: one compact PageHeader, SecondaryTabs, inner Tabs, and existing page actions.
- Stats treatment: existing Overview stats remain on Overview; no duplicated hero/stats wrapper is added to Simple Chats.
- List/editor organization: reusable workspace retains its collection and Wide definition dialog; the old routed PageScaffold is not nested.
- Textarea/dialog rationale: reuse SB07 Extended prompt sizing and Wide dense-chrome editor; no page-local sizing override.
- First viewport: PageHeader/tabs plus useful workspace content are visible at 1600x1000.
- Scroll owner: Agent page/workspace owns page content scroll; modal body owns modal scroll; floating windows do not create a page trap.
- Container proof: SecondaryTabs and inner Tabs are inspected full-width and inside the narrower Simple Chat workspace/dialog composition.
- Open-state proof: definition dialog plus Agent/Simple Chat floating windows verifies layering, clipping, focus restoration, and action visibility.

## C# Architecture Impact

Makes Modules.AgentFramework the thin product placement owner and removes Simple Chat’s separate module navigation/page ownership.

## Boundary Ownership

Agent module owns route/tab/navigation. Components owns reusable workspace/floating content. Runtime/Persistence remain composition-root services.

## Dependency Direction

Modules.AgentFramework -> Components/Application/Core. It must not absorb Runtime/Persistence implementations or feature internals.

## Pattern Decision

Typed route catalog and compatibility redirect adapter; reusable component composition.

## Testability Contract

Typed parsing/redirect/tab logic has unit/component proof independent of Playwright. Browser proof validates actual assembly/route/shell behavior.

## Partial Class Policy

Do not grow AgentsHomePage partial responsibility for Simple Chat internals. Extract typed route/catalog and small orchestration collaborators as top-level types.

## Architecture Proof Required

Before/after route/navigation/DI/project graph, direct owner tests, no duplicate registration/page, no-new-partial/cycle, browser proof, architecture gate.

## Progression Gate

- Page integration must pass before SB09 scopes the dashboard.

## Reopen Triggers

- tab order/key/state drift;
- redirect loses/overaccepts query state or loops;
- duplicate navigation/shell;
- provider editor divergence;
- Agent or Simple Chat main/floating regression.

## Covered Inputs

- Raw request: Simple Chats must be a tab immediately next to Agents while provider setup stays in AgentFramework.
- Requirements ASCC-003–004, ASCC-012–013, ASCC-016, ASCC-031–035, ASCC-039–043, ASCC-047.

## Exact Source References

- repo://src/Modules/CanDoItAll.Modules.AgentFramework/Pages/AgentsHomePage.razor
- repo://src/Modules/CanDoItAll.Modules.AgentFramework/Pages/AgentsHomePage.razor.cs
- repo://src/Modules/CanDoItAll.Modules.LlmChats.Ui/Pages/LlmChatsPage.razor
- repo://src/Modules/CanDoItAll.Modules.LlmChats.Ui/Navigation/LlmChatsShellNavigationContributor.cs

## Deliverables

- Canonical Agent page Simple Chats tab, typed query state, redirect-only /chats, canonical provider selection, and single floating/nav composition.

## Dependency Impact

- SB09 dashboard placement and SB10 legacy deletion require this route/composition to be complete; weak Agent parity blocks final closure.

## Acceptance Checklist

- All Acceptance Criteria above pass with tab adjacency, preserved inner tabs, route compatibility, one registration, provider visibility, and main/floating parity.

## Proof Required

- proof/SB08/manifest.md with component/named Playwright/MCP transcripts, semantic invariants, DI/navigation/source guards, screenshot hashes, architecture gate.

## Browser Validation Logging

- Routes: /agents?tab=simple-chats, /agents?tab=agents, and /chats with recognized state.
- Viewport: 1600x1000.
- Actions: inspect tab order/inner tabs, provider-to-definition flow, main Simple Chat, redirect/back-forward/reload, main/floating Agent and Simple Chat hide/reopen/cancel.
- Screenshots: SB08-agent-tabs, SB08-simple-chat-main, SB08-definition-editor-open, SB08-agent-floating-open, SB08-simple-chat-floating-open, SB08-chats-redirect.
- Review: first viewport, page/workspace scroll, nested/container tabs, modal/floating layering/focus, no clipping or console/page error.
