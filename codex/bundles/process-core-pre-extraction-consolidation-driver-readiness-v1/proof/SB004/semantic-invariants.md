# SB004 Semantic Invariants

## Invariants

- Invariant ID: `SB004-INV-001`
- Source raw note: `Continue progressive isolation steps leading toward Process Core while preserving all original functionality.`
- Expected behavior: Route-facing DTOs are pure read models without dispatcher source interfaces, while the application adapter edge can still recover dispatcher payloads for finalizer, recovery, direct-agent, and guard paths.
- Disallowed shallow implementation: Removing source payloads from DTO constructors while dropping or bypassing dispatcher-only behavior that still needs original payloads.
- Failing-first test: `N/A - behavior-preserving refactor; existing focused finalizer and route reload tests prove the dispatcher-only edges still work after the split.`
- Passing test: `bundle://proof/SB004/transcripts/route-dto-split-focused-integration-tests.txt`
- Changed source files: `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchRouteModels.cs`, `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchRouteModelAdapters.cs`
- Production assertions: `bundle://proof/SB004/transcripts/route-dto-source-assertions.txt`
- Red-team negative case: A route handler or route service calling `ProcessDispatchRouteModelAdapters.ToDispatcher*` would fail the source assertion.
- Downstream dependency check: `SB005` must confirm adapter confinement after this DTO split, and `SB006` must rerun route parity proof.

## Raw Note Closure

- Preserve existing behavior: `Partially solved by focused route/finalizer integration tests; broader route parity remains owned by SB006.`
- Move closer to Process Core: `Partially solved by pure route DTOs; remaining adapter confinement is owned by SB005.`
