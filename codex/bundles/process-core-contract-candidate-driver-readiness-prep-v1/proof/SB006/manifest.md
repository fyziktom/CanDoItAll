# SB006 Proof Manifest

## Scope
- Subbundle: SB006 - Gate B - route model adapter confinement.
- Changed source: `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.RouteExecution.cs`, `repo://tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs`.
- Bundle status files: `bundle://analysis/03-route-source-payload-usage-map.md`, `bundle://reviews/01-execution-report.md`, `bundle://subbundles/SB006/README.md`.

## Changed File Hashes
| Path | SHA-256 |
| --- | --- |
| `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.RouteExecution.cs` | `285684EC66E3070BB670ABB8CCBC147AEB018CF98667BF46DDCA93697FC9E08F` |
| `repo://tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs` | `942823AC584D4F3FA684E178ADA12B5CBA30B7F9BD57EFD60E00F08082BAF65A` |
| `bundle://analysis/03-route-source-payload-usage-map.md` | `1AFA955890BB3392DF761FD0AA13CC3905AA337D6FF68D9D9125C270D029FFD4` |
| `bundle://reviews/01-execution-report.md` | `5F05286259400C31E1A2832C69ABCD46C05319A2C28903C2526B0E52FE1CEEF2` |
| `bundle://subbundles/SB006/README.md` | `EB5DC8DF78B5DC258C032458F41850B01FA40C8366567E16990C065AF900426D` |

## Command Transcripts
- Passing proof: `bundle://proof/SB006/transcripts/route-boundary-architecture-tests.txt`
- Source assertions: `bundle://proof/SB006/transcripts/route-adapter-confinement-scans.txt`
- Anti-stub audit: `bundle://proof/SB006/transcripts/route-adapter-confinement-scans.txt`
- Hash proof: `bundle://proof/SB006/transcripts/changed-file-hashes.txt`
- Failing-first proof: N/A - process refactor with no behavior change; negative confinement is proved by `bundle://proof/SB006/transcripts/route-adapter-confinement-scans.txt`.

## Semantic Invariants
- Contract: `bundle://proof/SB006/semantic-invariants.md`
- Invariant ID: `SB006-INV-001`
- Test name: `Process_dispatch_route_service_model_decoupling_boundary_uses_route_models_and_narrow_services`
- Test name: `Process_dispatch_route_service_ownership_gate_uses_route_models_for_pre_execution_and_failure_closure`

## Source Assertions
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.RouteExecution.cs` uses `LoadRouteCandidateAsync` and no longer calls `ProcessDispatchRouteModelAdapters.FromDispatcherCandidate`.
- Route models, route execution models, route facets, route handlers, route pipeline, route handler factory, and route services have no `ProcessDispatchRouteModelAdapters` references.
- Remaining adapter usage is confined to `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchRouteModelAdapters.cs` and named edge compatibility callers documented in `bundle://analysis/03-route-source-payload-usage-map.md`.

## Gate Result
- Entry gate: Passed after SB005 closure.
- Closure gate: Passed with focused unit tests and source scans.
- Downstream dependency check: Finalizer, hydration, pre-execution, subprocess, direct-agent, projection, rule, and driver-readiness subbundles may proceed while this confinement proof remains valid.
