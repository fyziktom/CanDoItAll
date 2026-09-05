# SB06 — Coverage and test coupling audit

Status: **Not started**. Proof tier: **Behavioral**. Current authorization is documentation only.

## Objective and covered inputs

Verify complete behavioral replacement coverage and remove remaining private test coupling without inventing architectural count guards.

R-050–R-055/R-059; F08; all B rows coverage audit, especially B27/B29. See [requirements](../../requirements/00-normalized-requirements.md), [behavior matrix](../../requirements/02-behavior-preservation-matrix.md) and [accepted revision](../../inputs/03-accepted-review-and-revision-request.md).

## Prerequisites and exact source references

SB02–SB05 accepted with their own tests already migrated; complete behavior map and chosen final filters.

tests/Components/CanDoItAll.Tests.Components/AgentsHomePageTests.cs; AgentsHomePageTestExtensions.cs; AgentCatalogPanelTests.cs; six AgentDetailsDialog*Tests families and shared harness; WorkflowsPageTests.cs exact Agents_shell_exposes_workflows_page_navigation; added Unit/Components operation/dependency tests and touched descendant fixtures.

## Scope and deliverables

Complete old-behavior->new-test map; exact current selectors and expected named cases; public Workflows navigation test; durable direction/policy tests; clean affected harnesses.

## Implementation steps for later authorized execution

1. Audit B01–B30 and every retained/replaced historical behavior, including two history-host cases and selected chat context/descendant scenarios.
2. Replace the Workflows OpenWorkflows reflection case with a click on agents-shell-open-workflows and assert current navigation.
3. Remove remaining private-field/method/numeric-tab/uninitialized service assumptions from affected harnesses; preserve legitimate behavioral assertions.
4. Ensure durable boundary tests inspect actual type/dependency direction, not filenames, partial count, exact injection count or one syntax.
5. Rediscover final selected cases, reconcile changes before running, and reopen the owning implementation phase for any missed behavior or production correction.

## Dependency impact and do-not-do constraints

Test/helper cleanup only. Production contract corrections reopen SB02–SB05 instead of entering as unreviewed cleanup. Do not refactor unrelated workflow/provider suites.

Apply the [invariants](../../requirements/01-invariants-and-non-goals.md), [pattern decisions](../../architecture/03-csharp-pattern-selection-records.md), [UI composition contract](../../architecture/10-ui-composition.md) and [recovery/invalidation rules](../../plan/01-dependencies-reopen-and-invalidation.md). Do not start later phases on incomplete required proof.

## Validation depth and acceptance

Current route/primary/history/workflow filters plus exact selected context/child/new seam/composition cases. Run temporary source/test hygiene checks over expanded actual scope; review hits semantically. No new-test quota and no one-for-one count requirement. [Shared commands](../../commands/00-validation-commands.md) define reusable selectors; phase proof records the exact selected names/data cases and expected count before source edits, then actual discovery/results.

- [ ] Every required matrix behavior has meaningful current test/host evidence or an explicit characterized existing-defect disposition.
- [ ] Affected tests use public production seams and normally constructed fakes/adapters; no private shape or uninitialized service dependency remains.
- [ ] Exact Workflows button case passes and discovery matches the recorded named set.

## Proof and progression gate

Coverage map, selector/discovery reconciliation, temporary hygiene transcript, actual focused results and any owning-phase reopen outcomes. Store execution artifacts under proof/SB06; follow [proof placement](../../proof/README.md). No execution result is pre-filled.

Unlock SB07 only with complete focused coverage and no deferred seam/test repair.

## Reopen triggers

Any implementation contract/helper/test mapping changes or an omitted behavioral assertion is found.
