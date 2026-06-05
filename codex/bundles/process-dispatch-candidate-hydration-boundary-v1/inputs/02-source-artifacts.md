# Source Artifacts

| Artifact | Type | Durable reference | Purpose |
| --- | --- | --- | --- |
| Previous bundle execution report | Bundle proof | `repo://codex/bundles/process-dispatch-claim-route-boundary-v1/reviews/01-execution-report.md` | Confirms prior SB01-SB16 closure and N/A browser proof. |
| Previous final red-team scan | Bundle proof | `repo://codex/bundles/process-dispatch-claim-route-boundary-v1/proof/SB16/transcripts/sb16-final-red-team-scan.txt` | Provides current hotspot line counts and no-core/no-driver proof. |
| Next cutline | Architecture note | `repo://codex/bundles/process-dispatch-claim-route-boundary-v1/architecture/06-next-dispatch-cutline.md` | Names candidate selection and hydration as next safe seam. |
| Main dispatch loop | Production source | `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Dispatch.cs` | Contains candidate selection, hydration, route orchestration, start transition, and finalizer call. |
| Concurrency selection helpers | Production source | `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessAutomationExecutionRunSelection.cs` | Existing pure helper boundary to preserve. |
| Route snapshot helper | Production source | `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchRouteSnapshot.cs` | Existing local route facts boundary. |
| Route planner helper | Production source | `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchRoutePlanner.cs` | Existing route-kind helper. |
| Guard lease helper | Production source | `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchGuardLease.cs` | Existing in-memory step guard wrapper. |
| Start transition planner | Production source | `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchStartTransitionPlanner.cs` | Existing start-transition request builder. |
| Finalizer context factory | Production source | `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.FinalizerContextFactory.cs` | Existing route-specific finalizer context builder. |
| Architecture tests | Test source | `repo://tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs` | Guardrail surface for no-core/no-driver/no-dependency-drift checks. |
| Dispatch integration tests | Test source | `repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs` | Runtime behavior proof for candidate selection, hydration, route, and dispatch parity. |
