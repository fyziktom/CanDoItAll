# SB09 Semantic Invariants

- Invariant ID: `SB09_INV_001`
- Source raw note: RN-001 and RN-003.
- Expected behavior: The in-memory per-step dispatch guard serializes same-step work and removes released guards after the final lease releases.
- Disallowed shallow implementation: Leaving semaphore lifetime embedded in `DispatchAsync` or releasing the semaphore without cleaning the guard dictionary.
- Failing-first test: N/A - non-critical helper foundation; direct guard lease tests prove the boundary.
- Passing test: `bundle://proof/SB09/transcripts/sb09-guard-heartbeat-tests.txt`.
- Changed source files: `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchGuardLease.cs`, `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Dispatch.cs`.
- Production assertions: `bundle://proof/SB09/source-assertions/claim-heartbeat-session-boundary.md`.
- Red-team negative case: `bundle://proof/SB09/transcripts/sb09-anti-stub-and-scope-scan.txt`.
- Downstream dependency check: SB10/SB11 can assume dispatch guard lifetime is module-local and independent from route planning.

- Invariant ID: `SB09_INV_002`
- Source raw note: RN-001, RN-003, and RN-004.
- Expected behavior: Heartbeat renewal keeps outer and durable step leases alive, cancels dispatch when renewal fails, and surfaces `ProcessDispatchClaimLostException` with the step run id.
- Disallowed shallow implementation: Swallowing renewal failures, allowing dispatch to continue after claim loss, or moving heartbeat behavior into UI/browser proof paths.
- Failing-first test: N/A - non-critical helper foundation; existing heartbeat tests prove renewal and claim-lost cancellation.
- Passing test: `bundle://proof/SB09/transcripts/sb09-guard-heartbeat-tests.txt`.
- Changed source files: `repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs`.
- Production assertions: `bundle://proof/SB09/source-assertions/claim-heartbeat-session-boundary.md`.
- Red-team negative case: `bundle://proof/SB09/transcripts/sb09-anti-stub-and-scope-scan.txt`.
- Downstream dependency check: SB12/SB16 must keep claim-lost behavior explicit and non-UI.
