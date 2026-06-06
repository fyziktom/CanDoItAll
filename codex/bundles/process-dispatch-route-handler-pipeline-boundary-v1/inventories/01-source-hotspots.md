# Source Hotspots

| Source | Current role | Risk |
| --- | --- | --- |
| `ProcessRunAutomationDispatchService.Dispatch.cs` | Public dispatch facade plus residual subprocess/pre-execution/helper methods | Still too much route/helper ownership |
| `ProcessRunAutomationDispatchService.RouteExecution.cs` | Claimed route flow and route side effects | Next primary seam |
| `ProcessDispatchClaimLease.cs` | Claim store/coordinator and heartbeat entry | Good boundary; protect behavior |
| `ProcessRunAutomationDispatchService.ExceptionClosure.cs` | Claim-lost/heartbeat/generic failure closure | Needs context narrowing after route handler extraction |
| `ProcessDispatchRoutePipeline.cs` | Canonical route order | Must not drift |
| `ProcessRunAutomationDispatchService.Dispatch.cs::ProjectCompletedSubprocessArtifactsAsync` | Subprocess projection with EF and file side effects | Future seam / handler coordination |
| `ProcessRunAutomationDispatchService.Dispatch.cs::LoadDispatchCandidateAsync` | Candidate hydration and direct-agent binding | Future seam after route-handler split |
