# SCH-R1 — exhausted CRON must not prevent startup

## Established evidence

WF1 recorded an enabled fixed-date CRON that executed once, then caused replacement-host startup to fail with Quartz's no-first-fire exception. A narrowly guarded SQL pause restored the owned fixture; no production Scheduler fix was made. Native `SchedulerPlannerProjectionHostedService.StartAsync` awaits `SynchronizeAsync`. That loops all saved plans and calls `ScheduleJob` after rebuilding each trigger, without classifying the terminal/no-occurrence case. Thus one plan can abort projection and host startup (S01, S02).

The installed dependency is Quartz 3.13.1 (S03), not a reason for a package upgrade. In that version `ScheduleJob` validates/computes the trigger's first fire and rejects a missing result before storing it. `CronTriggerImpl` computes that value from its actual start and calendar. `GetNextFireTimeUtc` on a newly built, uncomputed trigger is not evidence of exhaustion. Consult Q1/Q2 in `external-sources.json` plus the actual restored package.

## Reproduction

Use an owned PostgreSQL database and real Quartz factory/hosted projection. Fix the relevant application and Quartz clocks or use controlled timestamps consistently; do not patch wall-clock assumptions in production. Build a valid fixed-year expression and time zone with one past occurrence, plus the exact stored native completed plan/run/admission state. Keep `IsEnabled=true`. Start the actual projection hosted service and capture the failure before the patch. Separately reproduce with a first real fire and restart of the same retained owned database when practical; this is required in final browser/host proof.

A test containing only a cron parser stub, a manually paused plan or swallowed hosted-service exception is not sufficient. Record before/after identities and counts. The prior report's fixture IDs are provenance, never a permission to mutate those historical records.

## Required semantics

Define a small explicit projection decision at the existing Scheduler owner. Distinguish disabled; valid with a real next/first occurrence; exhausted terminal/no-future plan; malformed or unavailable configuration; and unresolved/missed work under the installed misfire semantics. Keep this distinction separate from command acceptance and run outcome.

A completed terminal plan remains inspectable with its history and enabled intent; no future projection must not mean deletion of durable data or implicit re-enablement with an invented fire time. An exhausted schedule is not an invalid CRON syntax error. Conversely, a null future timestamp alone does not establish that a past occurrence completed, that an outstanding admission is safe to replay, or that the plan can be permanently disabled.

Preserve current DoNothing, FireAndProceed and IgnoreMisfire behavior for actual eligible missed/future work. Do not reinterpret them using a blanket `nextAfterNow is null => discard` rule. Record the treatment of explicit old StartAt, EndAt, last-year bounds, offset/DST and associated calendars. A changed/new target still uses the saved immutable Workflow version, original project authority and existing fire-admission checks.

Inspect the current delete-before-reschedule sequence. Compute/validate the replacement decision before needlessly destroying a healthy existing runtime projection when appropriate. Do not replace the job store, change durable fire IDs, add schema, rewrite the dispatcher or install a global retry loop. Keep unrelated failures observable; never blanket-swallow `SchedulerException`, DB failure, bad time zone or cancellation.

The UI must show no-next-fire/exhausted status truthfully using existing fields or a narrowly justified projection value, not a fabricated upcoming date. Save committed before projection still has its original known/unknown distinction. Repeated synchronizations/restarts must be idempotent with respect to completed execution.

## Required tests

| Test family | Actual proof |
|---|---|
| Terminal + neighboring future plans | Real startup succeeds with the enabled exhausted plan; future plan retains exact next fire; unrelated invalid/failed owner paths are not masked. |
| Repeated host restart | Native completed run and retained histories unchanged across at least two restarts; no duplicate dispatch or model/tool call. |
| Misfire matrix | All three policies over future, missed eligible, expired EndAt, fixed-year/no-future and explicit StartAt cases; differentiate unresolved admission from terminal work. |
| Time bounds | UTC plus a time zone/DST boundary from owned deterministic fixtures; no brittle local-machine zone assumptions. |
| Existing jobs | Disabled/removed/edited plans reconcile only their own runtime jobs, without deleting neighbors. |
| Writer/results | Stored ID and earlier commit remain visible if a later projection/Activity step fails; native read-back, not just a toast. |
| Final application | UI create one finite plan, let it finish, leave enabled, restart owned client without SQL repair, load History, then dispatch a future real plan exactly once. |

If existing semantics cannot decide a missed unresolved case, retain explicit unavailability/recovery and explain the boundary rather than silently inventing dispatch. Do not stop after mapping the already-known completed-plan defect.
