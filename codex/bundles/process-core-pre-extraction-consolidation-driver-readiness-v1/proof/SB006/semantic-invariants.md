# SB006 Semantic Invariants

## Invariants

- Invariant ID: `SB006-INV-001`
- Source raw note: `Preserve all dispatch behavior, route-stage behavior, finalizer behavior, retry/recovery/provider behavior, and subprocess/projection behavior while moving closer to Process Core.`
- Expected behavior: Route DTOs are source-payload-free, route handlers/services stay adapter-free, route order remains intact, start-transition reload still continues candidates when appropriate, and finalizer/direct-agent handoff still recovers dispatcher payloads at the application edge.
- Disallowed shallow implementation: A route DTO cleanup that compiles but loses dispatcher payload recovery, changes route order, bypasses start-transition reload, or lets adapter calls leak into route-facing services.
- Failing-first test: `N/A - no production behavior change was intended; this critical gate validates a behavior-preserving refactor.`
- Passing test: `bundle://proof/SB006/transcripts/route-parity-focused-integration-tests.txt`
- Changed source files: `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchRouteModels.cs`, `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchRouteModelAdapters.cs`, `repo://tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs`, `repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs`
- Production assertions: `bundle://proof/SB006/transcripts/source-assertions-and-scans.txt`
- Red-team negative case: A source-payload leak in `ProcessDispatchRouteModels.cs` or adapter call in `ProcessDispatchRouteServices.cs` fails the Gate B source scan.
- Downstream dependency check: `SB007` finalizer intent work depends on `bundle://proof/SB006/transcripts/critical-build.txt`, `bundle://proof/SB006/transcripts/focused-architecture-tests.txt`, and `bundle://proof/SB006/transcripts/route-parity-focused-integration-tests.txt`.

## Raw Note Closure

- Preserve route-stage behavior: `Solved for Gate B route DTO parity; later finalizer/hydration phases own their narrower parity gates.`
- Do not rush Process Core: `Partially solved by source-payload-free route DTOs without creating Core; final decision remains owned by SB036.`
- No production driver API: `Partially solved by Gate B source scans; final driver decision remains owned by SB033/SB036.`
