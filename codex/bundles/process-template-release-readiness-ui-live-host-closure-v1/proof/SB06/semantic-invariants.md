# SB06 Semantic Invariants

- Invariant ID: `SB06-INV-001`
- Source raw note: REQ-006 scheduler/workflow launch and verification job lifecycle.
- Expected behavior: SchedulerPlan and WorkflowRun origins start process runs through process-owned triggers, and read-only verification jobs preserve provenance and mutation-denial behavior.
- Disallowed shallow implementation: Scheduler or workflow code cannot call process drivers directly or mutate through verification jobs.
- Failing-first test: Direct-driver and mutation behavior is rejected by source scan plus read-only verification assertions.
- Passing test: `Target_launcher_starts_real_process_run`
- Changed source files: `repo://tests/CanDoItAll.Tests.Integration/ProcessesServiceIntegrationTests.cs`
- Production assertions: Integration tests assert target launch, workflow launch, completed-output handling, and verification job lifecycle.
- Red-team negative case: Source scan found no new scheduler/workflow direct-driver launch path.
- Downstream dependency check: SB07 matrix includes scheduler/workflow and verification job proof.
