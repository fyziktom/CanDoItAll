# SB10 Semantic Invariants

- Invariant ID: `SB10_INV_001`
- Source raw note: RN-001 and RN-003.
- Expected behavior: Start-transition request construction is isolated in a module-local planner without moving the transition side effect out of `DispatchAsync`.
- Disallowed shallow implementation: Creating a helper that only wraps `TransitionStepWithClaimAsync`, changes request fields, or hides transition failure/reload behavior.
- Failing-first test: N/A - non-critical planner foundation; focused request-field tests prove the source shape and behavior.
- Passing test: `bundle://proof/SB10/transcripts/sb10-start-transition-planner-tests.txt`.
- Changed source files: `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchStartTransitionPlanner.cs`, `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Dispatch.cs`.
- Production assertions: `bundle://proof/SB10/source-assertions/start-transition-and-fresh-skip-planner.md`.
- Red-team negative case: `bundle://proof/SB10/transcripts/sb10-anti-stub-and-scope-scan.txt`.
- Downstream dependency check: SB11/SB12 can assume start-transition request construction is pure while transition execution remains in the dispatcher.

- Invariant ID: `SB10_INV_002`
- Source raw note: RN-001, RN-003, and RN-004.
- Expected behavior: Fresh recovery redispatch skip decisions use route snapshot facts and preserve existing `runtime-recovery-scan` grace-period behavior.
- Disallowed shallow implementation: Re-encoding trigger strings in dispatch, skipping recoverable execution runs, or introducing browser/UI proof for runtime-only routing.
- Failing-first test: N/A - non-critical planner foundation; fresh-skip parity tests prove behavior against the existing selection helper.
- Passing test: `bundle://proof/SB10/transcripts/sb10-start-transition-planner-tests.txt`.
- Changed source files: `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Concurrency.cs`, `repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs`.
- Production assertions: `bundle://proof/SB10/source-assertions/start-transition-and-fresh-skip-planner.md`.
- Red-team negative case: `bundle://proof/SB10/transcripts/sb10-anti-stub-and-scope-scan.txt`.
- Downstream dependency check: SB11 route planning must call decision helpers without owning durable transition or execution side effects.
