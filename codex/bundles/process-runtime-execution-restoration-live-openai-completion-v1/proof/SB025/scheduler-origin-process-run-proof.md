# SB025 Scheduler-Origin Process Run Proof

## Status
Completed.

## Behavior Proven
- `SchedulerTargetLauncher` starts a real process run for `SchedulerPlanTargetKind.Process`.
- Scheduler-origin process runs use `StartRunFromTriggerAsync` with `SchedulerPlan` source metadata.
- Persisted process run trigger reason includes scheduler plan identity and `Requested by scheduler-planner`.
- Scheduler-origin process launch does not create workflow run links.

## Proof
- Focused integration transcript: `bundle://proof/SB025/transcripts/scheduler-origin-process-run-tests.txt`
- Source assertions: `bundle://proof/SB025/transcripts/scheduler-origin-source-assertions.txt`
- No transient bundle-path scan: `bundle://proof/SB025/transcripts/no-transient-bundle-path-scan.txt`
- Anti-stub/runtime-host drift scan: `bundle://proof/SB025/transcripts/anti-stub-and-runtime-host-drift-scan.txt`
