# CanDoItAll.Modules.SchedulerPlanner

## Purpose

Product module for scheduling workflow versions through scheduler-owned Quartz projection. It owns scheduler plans, run history, cron description, target launch orchestration, and the `/scheduler` Blazor page.

## Project Type

- SDK: `Microsoft.NET.Sdk.Razor`
- Target framework(s): `net10.0`
- Validation command:

```powershell
dotnet build src/Modules/CanDoItAll.Modules.SchedulerPlanner/CanDoItAll.Modules.SchedulerPlanner.csproj
```

## Dependencies

The authoritative project and package dependency list is in [CanDoItAll.Modules.SchedulerPlanner.csproj](CanDoItAll.Modules.SchedulerPlanner.csproj). This README focuses on the project's purpose, boundaries, and validation.

## Architecture Notes

Scheduler Planner coordinates Workflow launch through typed contracts. Process scheduler targets remain unavailable. `SchedulerPlannerDbContext` contains plans, display run history and retained fire admissions. The immutable canonical profile factory and existing physical Plan/Run mappings, legacy Automation columns and Plan/Run cascade remain unchanged. `SchedulerPlanner_FireAdmissions` has no Plan/Run cascade: a deleted or recreated plan must not erase the identity of a previously admitted fire. The complete `AppDbContext` remains the sole migration authority.

A fire freezes its exact Workflow version, canonical input, original correlation and saved authorizer ceiling before dispatch. Database claims are short and fenced by generation; provider execution is outside the transaction. The Workflow owner receives the preallocated run ID and stable caller key. Retry/recovery looks up that exact run and preserves accepted identity and post-admission observation failure. Separate background recovery observes accepted runs and expired interrupted claims; history/status reads have no launch or delivery side effects.

The first-party UI captures local operator authority. The managed Scheduler agent captures only an actual admitted governance ceiling; a legacy context without it remains unable to grant Structure effects. Scheduled execution retains the original authorizer channel and adds a distinct Scheduler fire binding. Current disable/delete/authority replacement is checked by a Scheduler-owned policy, with separate independent and transaction-enlisted APIs. Existing legacy terminal history stays readable. An attempted pre-cutover fire with no immutable snapshot must reconcile its actual saved Workflow lineage before redispatch; current plan input cannot stand in for its historical intent. Reauthorizing a saved plan grants future fires only and does not rewrite historical authority.

An admitted managed Agent in the default Sandbox can schedule a Workflow with an empty project ceiling. Admission, scheduling and result disclosure recheck the original actor, catalog generation, profile and current scheduling/tool permissions. Sandbox authority never adopts current project selection, all-project access, task/asset permissions or a Structure output target; Structure-only admission and status projection remain unavailable.

At actual new Workflow admission, the original Agent catalog lease is acquired before
SQL. The Scheduler policy locks the current plan row on the Workflow owner's enlisted
transaction; plan disable, deletion and authority replacement wait until that commit.
The source lease and SQL transaction are released before the backend executes. A known
accepted Workflow remains observable after later revocation.

Native output effects, operator reconciliation controls for legacy or ambiguous fires,
and explicit profile-transfer treatment of retained admissions remain dependent
integration obligations.

## Related Docs

- Repository overview: `README.md` at the repo root
- Process agent operator runbook: `docs/process-agent-operator-runbook.md`
