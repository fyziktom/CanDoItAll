> Implementation authorized by the owner on 2026-09-04. See inputs/04-implementation-authorization.md and reviews/02-execution-status.md. Documentation-only wording below records the preparation stage and does not block this authorized execution.

# SB02 — Workspace state and lazy read operations

Status: **Complete; owning and final integration gates passed**. Proof tier: **Behavioral**. Implementation authorized by inputs/04-implementation-authorization.md.

## Objective and covered inputs

Remove page persistence/aggregation while preserving current semantic state, lazy read regions and chat readiness.

R-020–R-022, R-030, R-041, R-050–R-053; F06/F07/F08; B01–B04/B27/B29. See [requirements](../../requirements/00-normalized-requirements.md), [behavior matrix](../../requirements/02-behavior-preservation-matrix.md) and [accepted revision](../../inputs/03-accepted-review-and-revision-request.md).

## Prerequisites and exact source references

SB01 route/lazy-load/context foundation accepted; exact chosen methods/data cases frozen before source edits.

src/Modules/CanDoItAll.Modules.AgentFramework/Pages/AgentsHomePage.razor and .razor.cs; Pages/AgentWorkspaceTabs.cs; Pages/AgentWorkspaceRouteState.cs; Services/AgentFrameworkUiServiceCollectionExtensions.cs; same-module workspace/query types; AgentsHomePageTests.cs and AgentsHomePageTestExtensions.cs; route/history-host and selected chat-context tests.

## Scope and deliverables

Typed workspace state and current route mapping; cohesive overview/usage operations with explicit demand; normal DI composition; migrated page tests and real-operation coverage.

## Execution steps

1. Freeze B01–B04 oracles, including both Providers/RequestHistory theory cases and selected AgentFrameworkModuleChatContextBuilderTests methods.
2. Introduce typed semantic state without adding URL behavior; preserve current route parameters, defaults and history replacement.
3. Move EF bound-resource counts and aggregation into cohesive operations. Preserve skipped aggregates on history hosts and independent usage-selection triggers/errors.
4. Move accessible selection/context readiness explicitly and retain other tab inputs; migrate page helper/tests in this phase.
5. Run focused route/page/history/context/real-query tests and actual registration integration; review page complexity and dependency direction.

## Dependency impact and do-not-do constraints

No new project references. Queries may depend on existing application/persistence through justified real boundaries; UI does not. Do not combine all reads into one eager aggregate.

Apply the [invariants](../../requirements/01-invariants-and-non-goals.md), [pattern decisions](../../architecture/03-csharp-pattern-selection-records.md), [UI composition contract](../../architecture/10-ui-composition.md) and [recovery/invalidation rules](../../plan/01-dependencies-reopen-and-invalidation.md). Do not start later phases on incomplete required proof.

## Validation depth and acceptance

Fresh production/unit/component builds as needed; route filter, AgentsHomePageTests, exact two history-host cases, selected Agents chat-context methods, and named new state/query/composition cases. Freeze names/counts first. Exercise Providers/RequestHistory -> Overview and selection -> context as dependent flows. [Shared commands](../../commands/00-validation-commands.md) define reusable selectors; phase proof records the exact selected names/data cases and expected count before source edits, then actual discovery/results.

- [x] Current URL codec behavior and both lazy-history host regressions pass.
- [x] Page has no direct EF/dashboard aggregation; query operations construct and execute under meaningful deterministic tests.
- [x] Context readiness and unaffected host panes retain correct inputs; test migration is complete for this seam.

## Proof and progression gate

Record actual method/data discovery, commands/results, before/after dependency and load-call evidence, normal query/DI construction, B-row outcomes and UI composition decision. Store execution artifacts under proof/SB02; follow [proof placement](../../proof/README.md). No execution result is pre-filled.

Unlock SB03 after workspace ownership, lazy reads and context results are proven. No reliance on later SB06 test repair.

## Reopen triggers

Route/context/state contract, query load timing, aggregation/error semantics, DI registrations or page helpers change.

Execution evidence: ../../proof/SB02/manifest.md and ../../reviews/02-execution-status.md. Final governed closure remains SB07; SB01 timing qualification is explicit.
