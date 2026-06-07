# SB005 Semantic Invariants

## Invariants

- Invariant ID: `SB005-INV-001`
- Source raw note: `Continue progressive isolation steps leading toward Process Core while preserving existing behavior.`
- Expected behavior: Route handlers and route services consume pure route DTOs only; dispatcher payload conversion is confined to named application-edge adapter files.
- Disallowed shallow implementation: Removing route DTO `Source` properties while letting route handlers or route services call dispatcher adapters directly.
- Failing-first test: `N/A - architecture confinement guard added after SB004 source split; no additional production behavior changed.`
- Passing test: `bundle://proof/SB005/transcripts/route-adapter-confinement-architecture-test.txt`
- Changed source files: `repo://tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs`
- Production assertions: `bundle://proof/SB005/transcripts/route-adapter-confinement-source-scans.txt`
- Red-team negative case: Adding `ProcessDispatchRouteModelAdapters.ToDispatcherCandidate` to `ProcessDispatchRouteServices.cs` fails the guard and scan.
- Downstream dependency check: `SB006` must rerun route DTO parity after this confinement guard passes.

## Raw Note Closure

- Move closer to Process Core: `Partially solved by route adapter confinement; parity proof remains owned by SB006.`
- Preserve existing behavior: `Partially solved by no additional production movement and focused guard proof; SB006 owns route parity tests.`
