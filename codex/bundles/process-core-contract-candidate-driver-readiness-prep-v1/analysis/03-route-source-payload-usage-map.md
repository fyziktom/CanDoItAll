# Route Source Payload Usage Map

## Summary
SB004 maps current uses of route-model source payloads before reducing them in SB005 and proving confinement in SB006.

## Route Source Payload Declarations
| Model | Source payload | Current purpose | Next action |
| --- | --- | --- | --- |
| `ProcessRouteCandidate` | `IProcessRouteCandidateSource Source` | Carries the dispatcher nested `DispatchCandidate` back to edge services that have not yet accepted a route-only contract. | Reduce direct consumers before SB006. |
| `ProcessRouteDispatchClaim` | `IProcessRouteDispatchClaimSource Source` | Carries the dispatcher nested `ProcessStepDispatchClaim` for recovery runtime and the finalizer adapter edge. | Keep only at named adapter/edge paths until later route-source payload reduction. |
| `ProcessRouteExecutionOutcome` | `IProcessRouteExecutionOutcomeSource Source` | Carries the dispatcher nested `DispatchExecutionOutcome` for finalizer adapter, guard, and retry/provider paths. | Reduce direct consumers during direct-agent and execution outcome phases. |

## Adapter Call Sites
| Source | Current adapter use | Owner |
| --- | --- | --- |
| `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.RouteExecution.cs` | Converts the initial claim to a route model at the route boundary; as of SB005 it loads candidates through `LoadRouteCandidateAsync` and no longer unwraps dispatcher candidates itself. | SB006 |
| `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchCandidateHydrationService.cs` | Converts loaded dispatcher candidate to `ProcessRouteCandidate`; still has a dispatcher-returning overload for legacy callers. | SB010-SB012 |
| `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchRecoveryRuntimeService.cs` | Converts route candidate/claim to dispatcher payloads before calling recovery delegate; converts outcome back to route model. | SB007-SB009, SB019-SB021 |
| `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchDirectAgentRuntimeService.cs` | Converts route candidate to dispatcher candidate before execution delegate; converts execution outcome back to route model. | SB019-SB021 |
| `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchCompetingExecutionGuardService.cs` | Converts route candidate/outcome to dispatcher payloads for competing execution guard delegate. | SB019-SB021 |
| `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchFinalizerAdapter.cs` | Converts route finalizer DTOs to dispatcher payloads and preserves legacy dispatcher finalizer calls; `ProcessDispatchFinalizerApplicationService` is route-facing and adapter-free as of SB009. | SB007-SB009 |
| `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchSubprocessRuntimeService.cs` | Converts route candidate to dispatcher candidate for subprocess runtime delegate. | SB016-SB018 |

## Non-Call-Site References
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchRouteModelAdapters.cs` owns all `FromDispatcher*` and `ToDispatcher*` conversions.
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchRouteServices.cs` uses route DTOs and does not directly reference `ProcessDispatchRouteModelAdapters`.
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchFinalizerApplicationService.cs` accepts route-owned finalizer input DTOs and delegates to `ProcessDispatchFinalizerAdapter` without dispatcher aliases or route-model conversion calls.
- `repo://tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs` has assertions that route services stay adapter-free and that the active bundle guard remains local.

## Proof
- Usage scan: `bundle://proof/SB004/transcripts/route-source-payload-usage-scan.txt`
- Guard scan: `bundle://proof/SB004/transcripts/guard-scans.txt`
