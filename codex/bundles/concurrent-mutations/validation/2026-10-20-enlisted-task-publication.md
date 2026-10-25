# Enlisted task publication and reopened responsiveness reference

Status: correctness prerequisite implemented; this run retains its failed performance reference.
The subsequent [source optimization](2026-10-20-workspace-path-probe.md) passes the same
reference; broader performance validation remains open.
Backend branch is `concurrent-mutations`; SPA remains on `codex/concurrent-mutations`.

The [design](../plan/P07-enlisted-task-publication.md) adds a required enlisted check to
the existing held-authority contract. The task publisher calls it after Project gates
and current basis validation, before staging, and again after flush before commit.
The ordinary Agent implementation delegates to its existing current check. No additional
catalog lease, transaction, SQL, dependency, retry or schema is introduced by that adapter.

Three new actual PostgreSQL publisher tests use the real selected Process fence and a
trusted prepared Project source. They prove expiry before staging, expiry after flush
rolling back tasks and receipt, successful publication, and receipt replay after expiry
without reacquiring mutation authority. This explicit test adapter does not claim that
the inherited Workbench material adapter or producer is activated.

## Validation

Release builds passed for Workbench (61.8316016 seconds) and Integration (67.6935116).
Exact discovery and execution were **73**, with **72 passed, 1 failed, 0 unexecuted**:

```text
FullyQualifiedName~ProjectTaskPlanProcessFenceTests|FullyQualifiedName~ProjectTaskPlanOperationTests|FullyQualifiedName~ProjectTaskPlanCurrentnessTests|FullyQualifiedName~ProjectTaskPlanActiveWorkTests|FullyQualifiedName~ProjectTaskPlanAgentIntegrationTests|FullyQualifiedName~ProcessDecisionSelectedPublicationTests|FullyQualifiedName=CanDoItAll.Tests.Integration.AgentPlanningPublicationCostTests.Small_selected_authority_keeps_unrelated_writers_responsive_during_actual_SQL_wait
```

All correctness cases passed. The selected publication guard performance reference
failed its unchanged one-second requirement: `same-cancel` took 1006.8473 ms and
`independent-grant` took 1066.3177 ms. All six writers completed during the actual SQL
wait, without operation failure; tasks and receipt remained absent. Workspace coordination
waits totalled 665.99 ms and 707.04 ms respectively. This is negative performance evidence,
not a timing waiver or a reason to rerun until green. The prior passing small-cohort
reference is historical; current responsiveness and wider capacity remain unqualified.

The full portability scan and final enforcement without baseline writing passed with
15,642 unchanged findings and no baseline delta. Baseline/secret tooling tests,
rendering tests/register, documentation and diff checks passed. Broad Stable was not
repeated; its historical failed/unexecuted results remain open.

Architecture snapshots `snap-20261020051805-de5e7b25` and
`snap-20261020054647-de5e7b25` retain six projects, 2581 types, 145 service registrations,
31 entities and 822 findings. Members increase 21839 to 21841; one existing type-size
finding changes. No new finding or project cycle; existing module/type cycles and 208
analyzer diagnostics are unchanged. Direct builds pass. The boundary change passes
architecture review; rollout and performance closure remain blocked by their own gaps.

## Recorded candidate and cleanup

Root `.artifacts/concurrent-mutations/validation/c4-enlisted-task-publication` is Released.
All 9592 source inputs and 1704 runtime files were verified unchanged after execution.
Source manifest SHA-256: `0CEF1BBAC109E53998264CB9A00F1C0286FA023C93774BED1B834B318F523683`.
Runtime manifest: `C30D915102489038537E5EAAEAC2E1C9526CA9F4FC377872DDD5D3BCCA87A9B6`.
The 383.2769341-second observer covered the entire 366.1902261-second command; its exact
process/start identity was confirmed before execution. No observed process or database
delta remains. The cost fixture explicitly confirms cleanup with no unknown allocation
or failure. Five unattributed TEMP paths were preserved. Shared PostgreSQL 18 on 53619
remains required; ordinary 5032, installed tooling and historical cleanup denials are unchanged.

[Retained UTF-8 LF proof](../proof/2026-10-19-planning-authority/c4-enlisted-task-publication.json)
SHA-256: `3E3419308B6B70C9E3C9D546A10FFBC509E8536355138FBFD007F886D2903BAF`.
It includes the failed performance result, individual timings and cleanup evidence.

Investigate and reduce measured file-workspace contention without weakening durable
recovery or the timing requirement. Continue inherited target/material/permissions,
parent-owned Core/MAF saved continuation, historical receipt access, actual producer,
atomic accounting, manager/Workflow integration and policy settings. R5/R6 stay open.
