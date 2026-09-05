> Implementation authorized by the owner on 2026-09-04. See inputs/04-implementation-authorization.md and reviews/02-execution-status.md. Documentation-only wording below records the preparation stage and does not block this authorized execution.

# SB03 — Controlled catalog and host effects

Status: **Complete; owning and final integration gates passed**. Proof tier: **Behavioral**. Implementation authorized by inputs/04-implementation-authorization.md.

## Objective and covered inputs

Make catalog rendering controlled and place catalog operations/host effects at explicit cohesive boundaries.

R-022/R-024, R-031–R-033, R-040–R-042, R-050/R-052/R-053/R-058; F01/F05/F06/F07/F08; B04–B08/B29/B30. See [requirements](../../requirements/00-normalized-requirements.md), [behavior matrix](../../requirements/02-behavior-preservation-matrix.md) and [accepted revision](../../inputs/03-accepted-review-and-revision-request.md).

## Prerequisites and exact source references

SB02 accepted; actual catalog call sites and host target/results characterized; chosen test map frozen.

src/Modules/CanDoItAll.Modules.AgentFramework/Pages/Components/AgentCatalogPanel.razor and .razor.cs; Pages/AgentsHomePage.razor and .razor.cs; same-module catalog operations/host coordination/DI; tests/Components/CanDoItAll.Tests.Components/AgentCatalogPanelTests.cs and AgentsHomePageTestExtensions.cs; actual existing team/member/detail dialog call sites.

## Scope and deliverables

Typed snapshot/selection/intents; external load/repair/team operation boundary; host-owned open/chat/result coordination; public tests; catalog-first extraction candidate assessment.

## Execution steps

1. Define select versus open/create/team/chat/repair intents and ownership; preserve local search/expansion and current initial-data/SkipCatalogRepair behavior.
2. Move feature I/O out of catalog and move host effects into page/workspace coordination or a justified narrow adapter; avoid a god page.
3. Preserve requested-ID open once, missing/changed requests, exact managed identities, context readiness and all team/member result/selection semantics.
4. Replace private openedRequestedAgentId and saved-handler tests with real page/catalog/host interactions; test real catalog operations normally.
5. Audit real card/list children and contract/assets graph. Produce a bounded catalog-only sandbox handoff with explicit host-intent simulation scope and owned blockers.

## Dependency impact and do-not-do constraints

Keep current project graph. No route change or sandbox/project implementation. Do not drag editor/team host dependencies into a catalog-only candidate by default.

Apply the [invariants](../../requirements/01-invariants-and-non-goals.md), [pattern decisions](../../architecture/03-csharp-pattern-selection-records.md), [UI composition contract](../../architecture/10-ui-composition.md) and [recovery/invalidation rules](../../plan/01-dependencies-reopen-and-invalidation.md). Do not start later phases on incomplete required proof.

## Validation depth and acceptance

AgentsHomePageTests/AgentCatalogPanelTests plus named real catalog/host/context cases mapped to B04–B08. Preserve AgentSelectionCard behaviors included in historical class. Execute page request -> catalog readiness -> open -> result -> selection reconciliation. Counts derived from exact selected tests. [Shared commands](../../commands/00-validation-commands.md) define reusable selectors; phase proof records the exact selected names/data cases and expected count before source edits, then actual discovery/results.

- [x] Catalog has no feature I/O, dialog or chat launch; local presentation state is allowed.
- [x] Selection/open/result/context and current confirmation policies are preserved through real host flows.
- [x] Real catalog operations are tested; candidate graph/handoff distinguishes catalog intents from full editor/team interaction.

## Proof and progression gate

Behavior map, exact tests/transcripts, before/after ownership, constructor and registration evidence, requested-open result traces, UI decision and sandbox candidate inventory. Store execution artifacts under proof/SB03; follow [proof placement](../../proof/README.md). No execution result is pre-filled.

Unlock SB04 with stable host target/result ownership. Candidate preparation may proceed independently of bookmarkability; separate implementation follows the handoff policy.

## Reopen triggers

Catalog public state/intent, host result semantics, repair/load triggers, selected-context mapping, or candidate type/child closure changes.

Execution evidence: ../../proof/SB03/manifest.md and ../../reviews/02-execution-status.md. Final governed closure remains SB07; SB01 timing qualification is explicit.
