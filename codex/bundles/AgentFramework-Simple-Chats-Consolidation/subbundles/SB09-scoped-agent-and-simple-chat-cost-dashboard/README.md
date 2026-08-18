# SB09 — Scoped Agent and Simple Chat cost dashboard

## Status

- Completed — CP3 Pass
- Stage: dashboard-checkpoint
- Proof tier: Governed

## Objective

Make the existing AgentFramework usage dashboard and detail dialogs switch predictably between Both, Agents, and Simple Chats using the neutral usage projection without changing catalog counts or consumer semantics.

## Owned Requirements

- ASCC-020
- ASCC-021
- ASCC-023
- ASCC-025
- ASCC-027
- ASCC-028
- ASCC-036
- ASCC-037
- ASCC-038
- ASCC-039
- ASCC-040
- ASCC-041
- ASCC-042
- ASCC-047

## Prerequisites

- SB08

## Current Source Anchors

- repo://src/Modules/CanDoItAll.Modules.AgentFramework/Pages/AgentsHomePage.razor
- repo://src/Modules/CanDoItAll.Modules.AgentFramework/Pages/AgentsHomePage.razor.cs
- repo://src/Modules/CanDoItAll.Modules.AgentFramework/Pages/Components/AgentUsageDialog.razor
- repo://src/Modules/CanDoItAll.Modules.AgentFramework/Pages/Components/ProviderUsageDialog.razor
- repo://src/Modules/CanDoItAll.Modules.AgentFramework/Pages/Components/ModelUsageDialog.razor
- target://src/MAF/Common/CanDoItAll.AgentFramework.Usage/

## Explicit Non-Goals

- Do not filter configured Agent/team/provider/capability catalog counts.
- Do not label Simple Chats as Agents.
- Do not query either persistence implementation directly from Razor.
- Do not add a fourth persisted Both producer.
- Do not redesign unrelated Agent tabs.

## Implementation Steps

1. Retry Components MCP and select an existing typed BaseLib scope control.
2. Add typed usageScope query mapping; default Both, normalize invalid values predictably, reject invalid service selections.
3. Separate catalog/runtime overview loading from neutral scoped usage loading; cancel/supersede stale scope responses.
4. Bind scoped usage observations, tokens, known cost, unpriced count, failures, provider chart, model chart, freshness/completeness.
5. Keep configured catalog totals unfiltered and label them.
6. Show Agent consumer rankings for Agents, definition/conversation rankings for Simple Chats, and source-specific typed sections/rows for Both.
7. Replace/migrate the Agent-only detail action with a typed consumer detail where Both/Chats requires it.
8. Pass the exact same selection to provider/model/consumer dialogs and preserve dialog scroll/loading/error states.
9. Add unit/component tests for all scopes, invalid state, stale response, unknown/unpriced, partial source, charts/dialog propagation.
10. Run named Playwright and Playwright MCP seeded/deterministic proof that fully attributed Both equals the two scoped contributions exactly once, unattributed legacy evidence is reported separately, and charts render nonblank.

## Acceptance Criteria

- [ ] Both is default and exact.
- [ ] Agents and Simple Chats scopes are disjoint.
- [ ] All usage metrics/charts/dialogs honor one typed selection.
- [ ] Catalog counts do not change.
- [ ] Unknown/unpriced/partial-source states are explicit.
- [ ] Consumer labels/links remain source-correct.
- [ ] CP3 browser/architecture gate Pass.

## Validation Depth

- Proof tier: Governed.
- Critical product checkpoint: yes; final cost/watchability claims depend on exact scope propagation.

Governed behavior/UI proof with deterministic usage fixtures, aggregation invariants, component tests, named Playwright/MCP screenshots/console/chart assertions, and architecture gate.

## Focused Test Selection

Workspaces:

- tests/Solutions/CanDoItAll.Tests.Unit.slnx
- tests/Solutions/CanDoItAll.Tests.Components.slnx
- tests/Solutions/CanDoItAll.Tests.Playwright.slnx

Required:

- ProviderUsageAggregationTests
- DashboardQueryServicesTests
- AgentsHomePageTests
- ProviderUsageDialogTests
- ModelUsageDialogTests
- AgentFrameworkSimpleChatsConsolidationPlaywrightTests.AllUsageScopesDriveChartsAndDialogs

Exact cases:

- BothIsDefault
- ScopeDoesNotChangeCatalogCounts
- UnknownAndUnpricedAreNotZero
- StaleScopeResponseCannotOverwriteCurrentSelection
- BothEqualsAttributedScopesAndReportsUnattributedSeparately

Expected discovery: every exact case/test selector discovers at least one test.

## Invalidation And Broad-Gate Decision

Stable/full Playwright forbidden. Named Playwright/MCP authorized. Reopen on selection/query/aggregate/dashboard metric/chart/consumer/dialog/loading state.

## UI Composition Contract

- Primary surface: scoped usage metrics, provider/model charts, and source-correct consumer rankings.
- Supporting content: scope selector, completeness/freshness state, and detail actions.
- Stats treatment: usage/tokens/cost/unpriced are compact task-supporting metrics; configured catalog counts remain separate.
- List/editor organization: no editor; provider/model/consumer details open in existing Wide dense-chrome dialogs.
- Textarea/dialog rationale: no new textarea; Wide dialogs retain comparison columns and own body scrolling with stable header/footer.
- First viewport: scope, compact usage metrics, and the start of both charts are useful before page scrolling at 1600x1000.
- Scroll owner: Agent page owns page scroll; dialog body owns modal scroll.
- Container proof: scope/compound controls must respond correctly in the overview grid, narrow card track, and dialog column on the wide desktop page.
- At 1600x1000 charts are nonblank, labels visible, and no overflow/overlay error exists.

## C# Architecture Impact

Agent page consumes a neutral usage query alongside Agent-specific catalog/runtime snapshot; persistence remains outside UI.

## Boundary Ownership

Usage owns scoped data semantics. Agent module owns presentation/orchestration. Components/Charts own controls/rendering.

## Dependency Direction

Page -> Usage service contract. No Page -> Agent file store or Simple Chat EF repository.

## Pattern Decision

Typed selection strategy and composite read model; source-specific consumer presentation.

## Testability Contract

Scope/query/render mapping tests use deterministic snapshots. Browser proof validates charts/dialogs but does not replace aggregate/unit assertions.

## Partial Class Policy

Do not expand AgentsHomePage partial with aggregation logic. Scope parsing/loading/presentation mapping belongs in cohesive top-level collaborators when non-trivial.

## Architecture Proof Required

Direct query owner proof, no UI persistence reference, selection propagation tests, before/after page dependencies, no-new-partial/cycle, browser artifacts, architecture gate.

## Progression Gate

- CP3 Pass unlocks SB10 legacy removal.

## Reopen Triggers

- catalog metrics filtered;
- Both mismatch/double count;
- chats labeled as agents;
- dialog scope mismatch;
- unknown/unpriced hidden;
- chart/browser/layout regression.

## Covered Inputs

- Raw request: existing Agent cost dashboard must switch Agents, Simple Chats, or Both.
- Requirements ASCC-020–021, ASCC-023, ASCC-025, ASCC-027–028, ASCC-036–042, ASCC-047.

## Exact Source References

- repo://src/Modules/CanDoItAll.Modules.AgentFramework/Pages/AgentsHomePage.razor
- repo://src/Modules/CanDoItAll.Modules.AgentFramework/Pages/AgentsHomePage.razor.cs
- repo://src/Modules/CanDoItAll.Modules.AgentFramework/Pages/Components/ProviderUsageDialog.razor
- repo://src/Modules/CanDoItAll.Modules.AgentFramework/Pages/Components/ModelUsageDialog.razor

## Deliverables

- Typed scope control/query loading, scoped metrics/charts/source-correct rankings, and consistently scoped provider/model/consumer dialogs.

## Dependency Impact

- SB10 must preserve this composition and SB11 must reproduce exact totals; query/render drift reopens SB06 and SB09.

## Acceptance Checklist

- All Acceptance Criteria above pass, including fully attributed equality, explicit unattributed/unknown/unpriced, stale-response safety, and dialog propagation.

## Proof Required

- proof/SB09/manifest.md with aggregate/unit/component/named Playwright/MCP transcripts, seeded expected totals, source assertions, screenshots/hashes, architecture gate.

## Browser Validation Logging

- Route: /agents?tab=overview with usageScope=both, agents, simple-chats.
- Viewport: 1600x1000.
- Actions: seed/produce deterministic Agent and Simple Chat evidence, switch all scopes, open provider/model/consumer dialogs, reload/deep-link, inspect charts DOM/SVG.
- Screenshots: SB09-both, SB09-agents, SB09-simple-chats, SB09-provider-dialog-open, SB09-unpriced-partial-state.
- Review: useful first viewport, catalog stat stability, exact metrics, source-correct labels, nonblank charts, constrained scope control, dialog scroll/layering, zero console/page errors.
