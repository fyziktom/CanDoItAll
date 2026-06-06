# Assumptions And Risks

## Assumptions

- Branch under implementation: `maf-processes-refactor`.
- This is a refactor-only bundle. No new business behavior should be added.
- The current working system must keep all existing dispatch functionality.
- The route order remains canonical:
  `FreshRecoverySkip -> DatabaseRequirement -> UpstreamMaterialization -> StrandedArtifactRecovery -> Subprocess -> StartTransition -> Workflow -> DirectAgentExecution -> CompetingExecutionGuard -> RunClosedGuard -> FinalizerTransition`.
- Browser, mobile, small-screen, medium-screen and Playwright visual proof are out of scope unless implementation unexpectedly touches UI, which it must not.

## Critical Path Risks

- Route handlers can accidentally change route order.
- Handler extraction can hide side effects in broad helper objects.
- `ProcessRunAutomationDispatchService` may leak back into coordinators through constructor injection.
- Failure closure may become incomplete if claim lost / heartbeat lost / generic exception paths are split incorrectly.
- Subprocess handling may lose capability-gap, artifact-projection, terminal-mirror, or parent-finalizer behavior.
- Workflow route handling may accidentally change workflow observation semantics.
- Direct-agent route handling may change competing execution, run-closed guard, or finalizer transition behavior.
- Codex may produce shallow wrappers without reducing coupling.

## Validation Risks

- Source scans must prove no route handler takes `ProcessRunAutomationDispatchService`.
- Tests must prove route order, route terminal behavior, failure closure and claim lifecycle.
- Build-only proof is insufficient.
- A single final report row is insufficient; every subbundle must have a row.

## Reopen Triggers

Reopen a prior phase if:
- any route handler depends on `ProcessRunAutomationDispatchService`,
- handler order differs from `ProcessDispatchRoutePipeline.StageOrder`,
- direct EF claim writes reappear in `Dispatch.cs` or route handlers,
- any production `IProcessDriverPack`, `ProcessDriverRegistry`, or `CanDoItAll.Processes.Core` token appears,
- any UI/viewport proof is created,
- subprocess artifact projection behavior changes,
- direct-agent finalizer behavior changes,
- failure closure path loses claim-held or run-closed guards.
