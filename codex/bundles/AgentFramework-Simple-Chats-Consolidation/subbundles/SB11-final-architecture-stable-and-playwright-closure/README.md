# SB11 — Final architecture, Stable, and Playwright closure

## Status

- Completed with documented one-shot Stable certification exception
- Stage: final
- Proof tier: Governed

## Objective

Freeze one final candidate and close the initiative with focused proof, one Stable gate, final architecture review, and end-to-end Playwright MCP validation of Agent and Simple Chat main/floating conversations and scoped costs.

## Owned Requirements

- ASCC-001
- ASCC-002
- ASCC-003
- ASCC-004
- ASCC-014
- ASCC-016
- ASCC-025
- ASCC-027
- ASCC-031
- ASCC-036
- ASCC-037
- ASCC-038
- ASCC-041
- ASCC-042
- ASCC-043
- ASCC-044
- ASCC-045
- ASCC-046
- ASCC-047
- ASCC-048
- ASCC-049
- ASCC-050
- ASCC-051
- ASCC-052

## Prerequisites

- SB10
- CP4 Pass

## Current Source Anchors

- repo://CanDoItAll.slnx
- repo://tests/Solutions/
- target://src/MAF/SimpleChats/
- target://src/MAF/Common/CanDoItAll.AgentFramework.Usage/
- repo://src/Modules/CanDoItAll.Modules.AgentFramework/
- repo://codex/bundles/AgentFramework-Simple-Chats-Consolidation/

## Explicit Non-Goals

- Do not add features or broad refactors.
- Do not repair unrelated Stable/Playwright failures without separate authorization.
- Do not rerun the one-shot Stable gate after a failure.
- Do not run full Playwright.
- Do not claim completion without both floating conversation sources.

## Implementation Steps

1. Freeze final candidate SHA/worktree diff hash; prohibit implementation changes except focused repair after a failing selector.
2. Run final CodeAnalytics health/dependency/cycle/impact analysis and source/DI/schema guards.
3. Run every subbundle’s required focused selector at the frozen candidate and record non-zero discovery/results.
4. Run the named AgentFrameworkSimpleChatsConsolidationPlaywrightTests only.
5. Start the supported watch/browser loop and use Playwright MCP against the real UI at 1600x1000.
6. Configure/use a deterministic provider through the Providers tab; never expose its secret in proof.
7. Create/open an Agent and send/complete a main chat; open floating Agent chat, send/stream/cancel where deterministic, hide/reopen, and verify transcript continuity.
8. Create/open a Simple Chat definition through the Agent page tab; verify Identity/Runtime/Output-and-revision tabs, choose/reset/upload/generate an avatar through the shared selector, save/reopen it, then send/stream/complete/cancel/reload a conversation and repeat floating Simple Chat hide/reopen continuity.
9. Open Agent settings and prove the same selector implementation retains Agent avatar behavior, including the configured-provider AI path or its explicit deterministic unavailable state.
10. Verify /chats redirect, tab adjacency, inner tabs, provider selection, Both default, Agents/Simple Chats/Both totals, exact no-double-count relation, charts, rankings, provider/model/consumer dialogs, deep-link/reload/back-forward, scroll/overlays, and zero unhandled console/page errors.
11. Capture screenshots/transcripts with sensitive data masked.
12. Run the unfiltered tests/Solutions/CanDoItAll.Tests.Stable.slnx exactly once.
13. Complete semantic invariants, checksums, final architecture gate, execution report, and user-verification handoff.

## Acceptance Criteria

- [ ] Frozen candidate and all focused selectors recorded.
- [ ] One Stable run recorded honestly.
- [ ] Named Playwright tests pass or an exact external/pre-existing blocker is documented.
- [ ] Playwright MCP proves main/floating Agent and Simple Chat flows.
- [ ] Both/Agents/Simple Chats cost scopes are exact and visually operational.
- [ ] Simple Chat settings tabs and the shared Agent/Simple Chat avatar selector—including AI generation—are browser-proven and persist only on save.
- [ ] No old projects/namespaces/cycles/duplicate registrations.
- [ ] Final architecture gate Pass and handoff complete.

## Validation Depth

- Proof tier: Governed.
- Final critical closure: yes; all user-visible and architecture claims converge here.

Governed final proof: immutable manifests, transcripts, screenshots, source/schema/DI invariants, SHA256 checksums, CodeAnalytics, named browser ledger, Stable transcript, architecture gate, and user handoff.

## Focused Test Selection

Workspaces:

- tests/Solutions/CanDoItAll.Tests.Stable.slnx
- tests/Solutions/CanDoItAll.Tests.Playwright.slnx

Named Playwright class:

- AgentFrameworkSimpleChatsConsolidationPlaywrightTests

Required cases:

- SimpleChatsTabImmediatelyFollowsAgents
- ChatsRouteRedirectsAndPreservesRecognizedState
- MainAndFloatingAgentAndSimpleChatFlowsRemainOperational
- AllUsageScopesDriveChartsAndDialogs
- SimpleChatSettingsTabsAndSharedAvatarWorkflowRemainOperational

Expected discovery: exactly the named cases exist and each discovers one test; Stable discovery is non-zero and recorded before the one authorized run.

## Invalidation And Broad-Gate Decision

SB11 alone authorizes one unfiltered Stable run. Full Playwright remains forbidden. Any post-freeze product change invalidates browser/focused proof and returns to the owning subbundle; Stable is not rerun without user authorization.

## UI Composition Contract

Complete plan/03-ui-composition-contract.md at 1600x1000.

- Primary surfaces reviewed: Agent overview/dashboard, Simple Chat workspace, Agent main/floating chat, Simple Chat main/floating chat.
- Supporting content/stats: verify compact page header/tabs/scope and that dashboard stats support rather than displace the primary task.
- List/editor organization: inspect definition/conversation lists and all three internal tabs of the Wide definition editor with realistic long prompt content.
- Textarea/dialog rationale: confirm Extended/explicit prompt rows, Wide dense-chrome dialog, stable actions, and dialog-body scrolling.
- First viewport: useful dashboard or workspace task state appears before page scroll.
- Scroll owner: record the one intended owner in every normal/open state.
- Container-aware controls: inspect top/inner tabs, scope selector, filter/action rows, and form groups in their actual narrow grid/card/dialog containers.
- Open overlays: inspect the shared avatar selector in both Agent and Simple Chat editors, scope selector, detail dialog, definition dialog, and both floating window sources for layering/clipping/focus.
- Record reload/deep links, accessibility state, nonblank charts, and browser console/page errors.

## C# Architecture Impact

No planned code impact; final audit of ownership, dependency direction, old-owner removal, testability, and compatibility.

## Boundary Ownership

Final source must match architecture/01-csharp-boundary-map.md with no legacy effective owner.

## Dependency Direction

Final CodeAnalytics and source guards prove architecture/02-csharp-dependency-direction.md and baseline-cycle non-enlargement.

## Pattern Decision

Audit PSR-001 through PSR-008 against implementation; any material divergence returns to its owner rather than being waived at final.

## Testability Contract

Direct unit/component/integration proof remains primary. Playwright proves composition/end-to-end behavior, not internal ownership by itself.

## Partial Class Policy

Final scan must show no new partial and reduced Agent usage partial responsibility.

## Architecture Proof Required

Complete before/after dependency/cycle graphs, direct source/test proof, old-owner deletion, no-new-partial, focused/Stable/browser evidence, final architecture gate.

## Progression Gate

- FINAL Pass closes the bundle. Otherwise record exact blocker and do not overstate completion.

## Reopen Triggers

- any product/schema/composition change after freeze;
- missing/zero-discovery selector;
- Agent or Simple Chat main/floating failure;
- cost scope/double-count/unknown-price failure;
- browser console/layout/accessibility failure;
- architecture/compatibility guard failure.
- settings-tab, shared-selector, avatar upload/generation/save, or Agent avatar parity failure.

## Covered Inputs

- Every raw request and ASCC-001–052 closure path, with direct ownership concentrated on the requirements listed above.

## Exact Source References

- C:\repositories\CanDoItAll\CanDoItAll.slnx
- C:\repositories\CanDoItAll\tests\Solutions\CanDoItAll.Tests.Stable.slnx
- C:\repositories\CanDoItAll\tests\Solutions\CanDoItAll.Tests.Playwright.slnx
- C:\repositories\CanDoItAll\src\Modules\CanDoItAll.Modules.AgentFramework
- C:\repositories\CanDoItAll\codex\bundles\AgentFramework-Simple-Chats-Consolidation\manifest.json

## Deliverables

- Frozen final proof set, one Stable result, named Playwright/MCP conversation/cost evidence, final architecture gate, checksums, and user handoff.

## Dependency Impact

- This is final closure. A failure reopens the owning earlier subbundle and invalidates downstream/final evidence; it is never hidden as residual risk.

## Acceptance Checklist

- All Acceptance Criteria above pass or the bundle remains honestly open/blocked with the exact missing evidence.

## Proof Required

- proof/SB11/manifest.md, semantic invariants, all focused/Stable/named Playwright/MCP transcripts, normal/open screenshots/hashes, final source/DI/schema/CodeAnalytics guards, verifier/red-team artifact, architecture gate, user handoff.

## Browser Validation Logging

- Routes: /agents overview/agents/simple-chats/providers plus /chats compatibility; relevant main/floating sources.
- Viewport: 1600x1000.
- Actions: configure deterministic provider safely, complete/cancel/reload Agent and Simple Chat main/floating conversations, switch every cost scope, open every relevant dialog, verify deep links/history.
- Screenshots: final overview Both/Agents/SimpleChats, main Simple Chat, each definition settings tab, shared avatar selector in Agent and Simple Chat contexts, AI generation/unavailable state, saved avatar, provider/model/consumer dialogs, Agent floating, Simple Chat floating, redirect, relevant error/unknown/unpriced state.
- Review: primary task in first viewport, stats density, long prompt/dialog sizing, all scroll owners, constrained compound controls, overlay/focus/clipping, nonblank charts, exact totals, zero unhandled console/page errors.
