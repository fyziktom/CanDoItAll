> Implementation authorized by the owner on 2026-09-04. See inputs/04-implementation-authorization.md and reviews/02-execution-status.md. Documentation-only wording below records the preparation stage and does not block this authorized execution.

# SB01 — Freeze baseline and characterize

Status: **Baseline accepted with documented watch timing limitation**. Proof tier: **Standard**. Implementation authorized by inputs/04-implementation-authorization.md.

## Objective and covered inputs

Freeze what exists, what is uncertain, and what must remain true before moving ownership.

R-001–R-005, R-039, R-050–R-051, R-058–R-059; F01/F02/F03/F07/F08/F09; all B01–B30 for inventory, especially U row B12. See [requirements](../../requirements/00-normalized-requirements.md), [behavior matrix](../../requirements/02-behavior-preservation-matrix.md) and [accepted revision](../../inputs/03-accepted-review-and-revision-request.md).

## Prerequisites and exact source references

A later implementation request, current repository instructions/testing/CI read, known working tree and sibling source mode. No prior phase.

All primary paths in inventories/00-source-scope.md; tests/Components/CanDoItAll.Tests.Components/{AgentsHomePageTests,AgentsHomePageTestExtensions,AgentCatalogPanelTests,AgentDetailsDialog*Tests,ProviderAdministrationLayoutTests,WorkflowsPageTests}.cs; tests/Unit/CanDoItAll.Tests.Unit/{AgentFrameworkSimpleChatsRouteTests,AgentFrameworkModuleChatContextBuilderTests}.cs; descendants/type owners in inventories/04-rendered-subtree-and-contract-closure.md. Braces denote named families to enumerate, not shell paths.

## Scope and deliverables

Source/dirty-state/sibling observations; exact test discovery; B01–B30 behavior-to-test map with named missing scenarios; characterized save/reset/load/result semantics; subtree/type/reference/assets baseline; current browser/iteration baseline; recorded existing defects.

## Execution steps

1. Refresh direct callers, service constructors, child scenarios and evaluated references; distinguish snapshot coverage from complete graph.
2. Discover historical primary/route/history-host/workflow anchors and selected chat-context/child cases. Map exact methods and data cases to B rows; add focused characterization tests for meaningful gaps before production edits.
3. Characterize B12 core-load UI/save eligibility and B16/B18/B20 reset/commit/close ordering. Keep pre-existing defects separate; resolve any ambiguity needed before its owning source phase.
4. Record representative normal/overlay host behavior and current watch measurement protocol/results from plan/03-sandbox-and-navigation-handoff.md. Revert isolated temporary timing edits.
5. Record the baseline gates and exact expected tests for SB02; no implementation starts on missing or unexplained relevant baseline failures.

## Dependency impact and do-not-do constraints

No retained production change, new projects, ownership moves or dependency updates. Characterization tests may be added in future execution; measure temporary edits only in an isolated reversible setup.

Apply the [invariants](../../requirements/01-invariants-and-non-goals.md), [pattern decisions](../../architecture/03-csharp-pattern-selection-records.md), [UI composition contract](../../architecture/10-ui-composition.md) and [recovery/invalidation rules](../../plan/01-dependencies-reopen-and-invalidation.md). Do not start later phases on incomplete required proof.

## Validation depth and acceptance

Run baseline discovery/build/focused selectors from commands; expected historical anchors 46/10/2/1 are observations to reconcile, not mandatory future totals. Characterization success/failure traces and real-host baseline complement tests. No full stable suite is required merely to document the baseline. [Shared commands](../../commands/00-validation-commands.md) define reusable selectors; phase proof records the exact selected names/data cases and expected count before source edits, then actual discovery/results.

- [x] Current behavior and required gaps are mapped by exact scenario/test names; baseline failures are resolved or explicitly scoped before dependent work.
- [x] No existing core-load defect is mislabeled as a new guarantee.
- [x] Subtree/type graph and watch/host baseline evidence are sufficient to select a bounded next step.

## Proof and progression gate

Store SB01 source observations, discovery/transcripts, behavior oracles, unresolved defect register, dependency inventory and timing samples. Standard proof remains concise and reproducible; no fabricated passing runtime claims. Store execution artifacts under proof/SB01; follow [proof placement](../../proof/README.md). No execution result is pre-filled.

Unlock SB02 only after its current route/lazy-load/context baseline is green and named. Unresolved unrelated defects do not authorize repairs; affected U behavior must be characterized before SB04.

## Reopen triggers

Source/sibling/test drift, omitted dependency/scenario, changed baseline assumption or measurement environment.

Execution evidence: ../../proof/SB01/manifest.md and ../../reviews/02-execution-status.md. Final governed closure remains SB07; SB01 timing qualification is explicit.
