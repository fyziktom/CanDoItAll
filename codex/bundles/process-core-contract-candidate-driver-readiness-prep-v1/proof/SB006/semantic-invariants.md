# SB006 Semantic Invariants

## Invariant SB006-INV-001
- Invariant ID: `SB006-INV-001`
- Source raw note: Preserve behavior while moving closer to Core and future drivers without creating Core or driver APIs.
- Expected behavior: Route-facing handlers and services operate on route DTOs and do not unwrap dispatcher candidates, claims, or execution outcomes; compatibility conversions remain named and edge-local.
- Disallowed shallow implementation: Moving `ProcessDispatchRouteModelAdapters` calls from route services into route handlers, or keeping hidden dispatcher nested-model references in the route-facing source set.
- Failing-first test: N/A - process refactor with no behavior change; negative source scan is `bundle://proof/SB006/transcripts/route-adapter-confinement-scans.txt`.
- Passing test: `bundle://proof/SB006/transcripts/route-boundary-architecture-tests.txt`
- Changed source files: `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.RouteExecution.cs`, `repo://tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs`
- Production assertions: `bundle://proof/SB006/transcripts/route-adapter-confinement-scans.txt` proves the route-facing source set has no adapter or dispatcher nested-model references.
- Red-team negative case: The confinement scan would list any reintroduced `ProcessDispatchRouteModelAdapters` or dispatcher nested-model token in route handlers/services and fail this gate.
- Downstream dependency check: SB007-SB030 depend on the route-facing boundary remaining adapter-free before finalizer, hydration, subprocess, direct-agent, projection, validation, pure-rule, and driver-readiness work continues.
