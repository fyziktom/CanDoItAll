# SB027 Semantic Invariants

## Status
Completed.

## Invariant SB027_INV_001
- Invariant ID: `SB027_INV_001`
- Source raw note: processes must run from scheduler/workflow-origin paths, not from driver runtime hooks.
- Expected behavior: Scheduler-origin process runs and workflow-origin process runs use `StartRunFromTriggerAsync` with typed trigger source metadata, preserve provenance in persisted trigger reason, enqueue runtime outbox records, and reject missing workflow source identity.
- Disallowed shallow implementation: Counting manual starts, scheduler plan persistence, or workflow target launch without process trigger-origin provenance.
- Failing-first test: `StartRunFromTriggerAsync_SB038_INV_002_rejects_workflow_trigger_without_source_identity` rejects missing trigger source identity/requester.
- Passing test: Four focused integration tests passed in `bundle://proof/SB027/transcripts/trigger-origin-process-starts-tests.txt`.
- Changed source files: No production source changed in SB027. Current source/test hashes are captured in `bundle://proof/SB027/manifest.md`.
- Production assertions: `bundle://proof/SB027/transcripts/trigger-origin-source-assertions.txt`
- Red-team negative case: `bundle://proof/SB027/red-team/manual-start-not-trigger-origin-proof.txt`
- Downstream dependency check: UI proof may start because trigger-origin runtime paths are source-backed.

## Shallow-Pass Trap
A fake Gate I closure could reuse manual run-start tests or scheduler save tests. SB027 rejects that by requiring scheduler target launch to a real process, workflow-origin `StartRunFromTriggerAsync`, scheduler workflow launch distinction, and missing-source validation.

## Semantic Positive Proof
- `bundle://proof/SB027/transcripts/trigger-origin-process-starts-tests.txt`
- `bundle://proof/SB027/transcripts/trigger-origin-source-assertions.txt`

## Adversarial Negative Proof
- `bundle://proof/SB027/red-team/manual-start-not-trigger-origin-proof.txt`

## Anti-Stub Audit
- `bundle://proof/SB027/transcripts/anti-stub-and-runtime-host-drift-scan.txt`
- Matches are documentation and negative test assertions, not an execution-capable process-driver runtime host, process-driver registry, selector, or production `NotImplemented` path.

## Production Behavior Artifact Matrix
| Artifact | Producer | Consumer | Lifecycle | Negative proof |
| --- | --- | --- | --- | --- |
| SchedulerPlan process trigger | `SchedulerTargetLauncher` | Process runtime | Calls `StartRunFromTriggerAsync` with `SchedulerPlan` source and scheduler requester | Manual start proof is rejected |
| WorkflowRun process trigger | `StartRunFromTriggerAsync` | Process runtime | Requires source ID and requester, persists provenance | Missing source identity test rejects weak trigger requests |
| Trigger runtime outbox | Process runtime start | Outbox/dispatch worker | Trigger-origin starts enqueue start-run and automation dispatch work | No driver runtime hook permitted |
