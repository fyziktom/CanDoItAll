# Durable Process accounting checkpoint

Implements the [accounting transaction design](../plan/P07-process-budget-persistence.md)
after signed shared-boundary checkpoint `f2f834c14`. All 41 selected cases pass on one
frozen candidate. Process/Workflow policy activation remains closed.

## Behavior and ownership

The actual initial runtime commit creates one budget for each policy-bearing assignment,
using the original effective ceiling. Legacy assignments create none. An owner-issued
budget identity and unique run/step key prevent replacement during retry or rework.
The balance and exact allocation are separate indexed rows, bounded to 256 retained
allocations per lineage by the shared arithmetic and schema constraints.

An explicit typed reservation or settlement accompanies an applied runtime mutation.
The persistence collaborator stages it in the same DbContext, root SQL fence and
transaction as state/events/outbox/idempotency. It owns no transaction, clock, provider
call, external journal read or SaveChanges. The unit of work retains its existing CAS.
Closed actual work can settle after cancellation; overrun remains charged and sticky.

The bounded canonical receipt preserves the original accounting command and result.
Replay validates original parameters, including added/removed accounting, and returns
the original balance/allocation separately from current runtime state. Lost commit
acknowledgement therefore does not reserve twice. Missing historical accounting fails;
it is never reconstructed from current settings. Nullable command/result fields are
omitted from legacy serialization. Prepared launches reject an accounting command.

The additive `20261019172603_AddProcessDecisionBudgets` migration creates two tables,
indexes/constraints and a nullable receipt column. No existing runtime payload is
rewritten. Downgrade takes table locks and refuses to discard any retained accounting.
The generated model has no pending changes; the idempotent upgrade script was inspected.
Real PostgreSQL tests apply generated Up/Down operations, preserve a legacy runtime and
verify refusal before evidence is dropped. This is not a production deployment.

## Validation

Standard Release Runtime, Persistence, Web (including Migrations) and Integration builds
pass, using local libraries and serialized `/m:1`. Existing compiler/advisory warnings
remain. The final executable source was built before discovery and freeze.

| Exact bounded selection | Discovered / passed | Command seconds |
| --- | --- | --- |
| `FullyQualifiedName~ProcessDecisionBudgetPersistenceTests` | 21 / 21 | 74.756 |
| `FullyQualifiedName~Decision_policy_survives_preparation_commit_reload` | 2 / 2 | 17.843 |
| `FullyQualifiedName~ProcessExecutionAuthorityPersistenceTests` | 18 / 18 | 89.138 |

The new cases cover initial/legacy admission, restart and historical replay, changed
command rejection, rollback after SQL flush for both initial and subsequent commits,
concurrent reservations/exhaustion, foreign step/budget and stale revision, cancellation
and overrun settlement, independent-root progress, exact allocation/settlement replay,
lost acknowledgement, changed closed evidence, missing-history refusal and migration
retention. Existing source/project/profile/claim authority remains protected.

The observed allocation command issues exactly two accounting SELECTs with exact head
and allocation predicates backed by the new keys. It does not enumerate siblings.
Another root completes while one commit is deliberately held. These are correctness
and query-shape checks, not an EXPLAIN/capacity benchmark or throughput qualification.

Full portability-static enforcement passes with 15,619 reviewed findings. The four
added findings are generated migration copies of `RootPathState`, `RootPathSyntax`,
`RaisesEscalation` and `EscalationCount` property metadata, with no machine path or
platform execution introduced. The complete scan included untracked source. Every
delta was inspected, the baseline diff contains only those four additions and metadata,
and final enforcement ran without `--write-baseline`. Rendering, documentation and
`git diff --check` pass.

## Architecture gate

Status: **Pass for durable local Process accounting; activation remains open.**

Before `snap-20261019172048-d1ce2c09`: four projects, 946 types, 8,689 members,
22 entities, 319 findings, 190 diagnostics. After `snap-20261019173059-d1ce2c09`:
four projects, 958 types, 8,768 members, 24 entities, 321 findings, 190 diagnostics.
Dependencies are unchanged and there are zero cycles. The two new informational
findings are cohesive data records: the 14-field balance entity and private 20-field
receipt document. Existing workspace-load diagnostics remain; actual builds pass.

Runtime owns typed commands and receipts. SharedKernel still owns the independently
tested arithmetic. Persistence owns storage, canonical encoding and atomic staging.
The existing unit of work delegates through a context-bound collaborator; no interface,
service locator, generic hook, provider dependency or runtime partial was added. The
generated EF migration partial is tooling output. No SPA files or branches changed.

## Evidence and resources

Candidate: `.artifacts/concurrent-mutations/validation/c4-process-budget-persistence`.
Source: 9,428 files, SHA-256
`57247B4A40A363F98B3555261A56A92EED5F28375A82D0B189C2D66C5892CA4F`.
Runtime: 3,194 files, SHA-256
`F7FC28F594CDDA3A692192C4969EB032FF6F8D5500428F45DD6C7E1E779E349B`.
Complete identity verification passed before release. Compact proof:
`proof/2026-10-19-planning-authority/c4-process-budget-persistence.json`, SHA-256
`FFEC39C934C02032595781C5CEFF9C2F368DB38D28EEE6FFDED9A0A1697E1555`.

Ownership was recorded before creating the candidate. The existing isolated PostgreSQL18
fixture was reused. Observation covers all three complete test commands, with zero
remaining observed processes and zero database-name delta. Twelve unattributed TEMP
entries are preserved. No old blocked cleanup was retried; zero new delta does not
close those earlier blockers. The ordinary instance on 5032 is untouched. The freeze
was released only after identity, observation coverage and resource reconciliation.

## Remaining runtime work

This is durable accounting for a local logical-step lineage. Actual Agent/Workflow
settlement verification, nested allocations, root-wide admission capacity, material
inheritance, decision-generation enrollment/fencing, manager continuation and producer
settings are not implemented by this checkpoint. Stored values are internal contracts,
not trusted HTTP/provider settlement bodies or permission to publish. These runtime
gates, the wider owner perimeter, performance qualification and the previously incomplete
Stable suite remain open; no end-to-end completion is claimed.
