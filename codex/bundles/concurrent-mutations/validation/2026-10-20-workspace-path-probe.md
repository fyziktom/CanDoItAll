# Exact workspace path policy and responsiveness repair

Status: implemented and validated after `e4be2d239`. Backend remains on
`concurrent-mutations`; SPA remains on `codex/concurrent-mutations` without merge or push.

The [design](../plan/P04-workspace-path-probe-contention.md) removes repeated filesystem
case-probe file creation/deletion from the file workspace store's own generated paths.
The existing Infrastructure factory accepts an explicit comparison mode. The store selects
ordinal containment; general callers retain the parameterless detected mode. Unknown and
undefined configured modes are rejected. Per-call explicit mode remains supported.
This is an exact-path contract, not a cache or claim about the underlying filesystem.

Fresh policy construction, native-root ancestor/link checks, containment, mutation
revalidation, OS locks, durable flushes, recovery journals and commit stages remain intact.
No schema, project reference, new runtime type, partial, service locator or registry was
added. The new actual symlink-replacement test executes affirmatively on this Windows host.
Existing platform-conditional Unix branches do not constitute Linux validation here.

## Validation and measured effect

Standard Release builds passed: Infrastructure 4.2647544 seconds, Persistence 12.3258108,
Unit 93.1350524 and Integration 40.6431652. Exact built discovery matches execution:

| Filter | Discovered | Passed | Failed |
| --- | ---: | ---: | ---: |
| `PhysicalFileSystemPathPolicyTests` / `DurableFileWriterTests` / `DurableFilePublicationTests` | 46 | 46 | 0 |
| `FileSandboxWorkspacePreparedCommitReadIntegrationTests` / `FileSandboxWorkspacePublicationRecoveryTests` / exact selected cost reference | 29 | 29 | 0 |

Each class filter is `FullyQualifiedName~<class>` joined with `|`. The exact cost filter is
`FullyQualifiedName=CanDoItAll.Tests.Integration.AgentPlanningPublicationCostTests.Small_selected_authority_keeps_unrelated_writers_responsive_during_actual_SQL_wait`.
The unchanged probe and one-second requirement now pass after the source change:

| Writer | Previous failed cohort (ms) | Revised cohort (ms) |
| --- | ---: | ---: |
| Same workspace checkpoint | 269.32 | 162.67 |
| Same workspace cancellation | 1006.85 | 433.29 |
| Same workspace grant | 748.33 | 630.63 |
| Independent workspace checkpoint | 697.84 | 429.36 |
| Independent workspace cancellation | 943.65 | 670.72 |
| Independent workspace grant | 1066.32 | 281.50 |

All writers finish while the actual SQL holder remains. The selected authority is held
5041.1393 ms; the publisher safely refuses without tasks or receipt. SQL command count
remains 10 at the Workbench interceptor. Cohort allocations are 238109632 bytes, measured
process-wide rather than per operation or peak memory. Files are small (run 3669 bytes,
catalog about 816 KB); the authority remains synthetic around real selected exclusion.
The earlier failed cohort is retained in the [enlisted-hook record](2026-10-20-enlisted-task-publication.md).
This single revised cohort is not p95, sustained capacity, maximum-size or fairness proof.

Full portability scan and final no-write enforcement pass: 15642 unchanged findings,
no added/stale findings, no baseline edit. Baseline/secret tooling tests, rendering
tests/register, documentation and diff checks pass. Historical broad Stable failures
and unexecuted cases remain open; no broad rerun was warranted for this bounded change.

## Architecture and evidence

Snapshots `snap-20261020060241-9a1b7d92` and `snap-20261020060641-9a1b7d92` cover
Infrastructure and AgentFramework.Persistence: two projects, 684 types, 69 registrations,
eight entities and 182 findings unchanged. Members increase 4820 to 4824. No new finding;
existing file-size and type-source-location findings change. No project cycles; the existing
module and type cycles are unchanged. Existing 199 analyzer diagnostics remain; direct
builds pass. Architecture review passes for this factory contract and owner configuration.

Root `.artifacts/concurrent-mutations/validation/c4-workspace-path-probe` is Released.
9595 source inputs and 2926 runtime files were verified unchanged after execution.
Source manifest SHA-256: `E422A63AFA5BC1648D7EC44DAD01C2C42235FAB9B101BC14778830E9E99BF009`.
Runtime manifest: `30592C43286084899DC1F2BB09294B956A05A79080707EADD7FD5B9434CA3AA1`.
The observer's exact process/start identity was verified and its 81.3473095 seconds cover
both commands (Unit 3.7851532, Integration 52.1700875). No observed process or database
delta remains. The measurement fixture explicitly confirms cleanup. Six unattributed TEMP
entries are preserved; they are not deletion authority. Shared PostgreSQL 18 on 53619
remains needed. Ordinary 5032, installed tools and historical cleanup denials are preserved.

[Retained UTF-8 LF proof](../proof/2026-10-19-planning-authority/c4-workspace-path-probe.json)
SHA-256: `B7244BA5C613F6CFCED1962DF0DED109C6B9DE8C9B4927FBD3B4787BD2947868`.

The reopened small-cohort reference is repaired by a source change and reviewed tests.
Continue parent-authorized saved continuation, inherited business/source integration,
actual producer and atomic outcomes, Workflow/manager/settings activation and capacity.
R5/R6 and end-to-end closure remain open.
