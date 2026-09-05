# SB05 — Editor operations, adapters and child boundaries

Status: **Not started**. Proof tier: **Behavioral**. Current authorization is documentation only.

## Objective and covered inputs

Move external editor work into real testable operations while preserving mutations, data semantics and real child workflows.

R-034–R-039/R-041–R-046/R-052/R-053; F02/F03/F05/F06/F08; B10–B26/B29. See [requirements](../../requirements/00-normalized-requirements.md), [behavior matrix](../../requirements/02-behavior-preservation-matrix.md) and [accepted revision](../../inputs/03-accepted-review-and-revision-request.md).

## Prerequisites and exact source references

SB04 session/load/host contract accepted; exact mutation/failure/version/child scenario map and constructor boundaries frozen.

src/Modules/CanDoItAll.Modules.AgentFramework/Pages/Components/AgentDetailsDialog.razor.cs and .razor; same-module editor operations/policies/adapters/DI; necessary same-module descendant seams listed before editing; tests/Components/CanDoItAll.Tests.Components/AgentDetailsDialog*Tests.cs and selected child tests; new real operation/adapter tests in existing projects.

## Scope and deliverables

Pure normalization/permission policies; cohesive load/reference/save/delete/capability operations; meaningful ports/adapters with normal constructors; explicit committed-versus-refreshed outcomes; preserved version/session/results; fully tested owned descendants.

## Implementation steps for later authorized execution

1. Separate pure rules from I/O; audit public result type assemblies and use justified narrow UI projections where Projects/Security implementation DTOs would retain coupling.
2. Implement operations with correct ExpectedUpdatedAtUtc and returned identity/version. Snapshot/freeze mutable inputs according to characterized UI behavior.
3. Preserve whole-draft capability writes for existing agents, staged new draft assignments, wizard effects, ordinary save staying open, Clear/create/save-again and exact delete result channel.
4. Test pre-commit failure, conflict, indeterminate outcome where relevant, committed mutation then refresh/callback failure, and stale publication. Never silently replay a committed mutation.
5. Complete required real child workflows, normally construct actual operations/adapters, migrate affected tests immediately and verify production registration/integration.

## Dependency impact and do-not-do constraints

No interface quota. No full-host-only god controller or raw service bag. Same-module ports/projections require responsibility/test evidence; cross-module contract moves/sibling/project changes remain separately scoped.

Apply the [invariants](../../requirements/01-invariants-and-non-goals.md), [pattern decisions](../../architecture/03-csharp-pattern-selection-records.md), [UI composition contract](../../architecture/10-ui-composition.md) and [recovery/invalidation rules](../../plan/01-dependencies-reopen-and-invalidation.md). Do not start later phases on incomplete required proof.

## Validation depth and acceptance

Named pure policy, real operation, adapter/composition and public component cases for B10–B26/B29; selected descendants including memory/root/storage/provider/avatar/capability. Verify actual calls/results/versions, not only fake controller return values. Run fresh affected builds and exact discovery. [Shared commands](../../commands/00-validation-commands.md) define reusable selectors; phase proof records the exact selected names/data cases and expected count before source edits, then actual discovery/results.

- [ ] Parent direct feature/infrastructure I/O is removed without hiding it in a locator/partial.
- [ ] Save/delete/capability/version/reset/partial failure semantics and all required descendant behaviors pass.
- [ ] Real new operations/adapters construct normally, preserve production application boundaries and have meaningful tests.
- [ ] Known external graph blockers are owned and readiness remains qualified.

## Proof and progression gate

Command outcome traces, before/after dependency/public-type audit, exact test/artifact links, operation/adapter integration, stale/instance/version failures, UI composition and B-row coverage. Store execution artifacts under proof/SB05; follow [proof placement](../../proof/README.md). No execution result is pre-filled.

Unlock SB06 only after this phase's behavior gaps and test migrations are closed. An unresolved required workflow is not a deferred sandbox limitation.

## Reopen triggers

Normalization, permissions, expected version, command/result channel, reference projection, child dependency, constructor or lifetime changes.
