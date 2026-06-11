# SB06 Semantic Invariants

## Invariant SB06_INV_001
- Invariant ID: `SB06_INV_001`
- Source raw note: Continue toward generic runtime host without unsafe side effects.
- Expected behavior: Runtime-host manager readback and dry-run denial readback must attach to a real process run and step id created through representative process-mock automation.
- Disallowed shallow implementation: Do not prove readback with an isolated fake run id, direct mapper-only unit test, or status-only assertion.
- Failing-first test: `bundle://proof/SB06/transcripts/failing-first-source-assertion.txt`
- Passing test: `bundle://proof/SB06/transcripts/focused-integration.txt`
- Changed source files: `repo://tests/CanDoItAll.Tests.Integration/ProcessDomainEvidenceReadOnlyAdapterTests.cs`; `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessManagerReadOnlyVerificationReadback.cs`; `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessManagerRuntimeHostDryRunReadback.cs`; `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDryRunExecutionPipeline.cs`.
- Production assertions: The test maps business-specific roles, invokes `IProcessManagerReadOnlyVerificationFacade.VerifyForReadbackAsync`, asserts the real `ProcessRunId` and `StepRunId`, verifies audit id/hash/evidence count, and checks all mutation permissions remain false.
- Red-team negative case: `bundle://proof/SB06/transcripts/side-effect-scan.txt` reports no process, transition, finalizer, local command, file write, HTTP, or dispatch mutator API usage in scoped host/readback paths.
- Downstream dependency check: SB07 can reuse the manager facade and read-only contract because SB06 proves facade readback and denied dry-run projection without mutation.

## UI Gap
- No existing run-detail UI exposes this readback. The bundle records API/facade proof and an explicit UI gap for later run-detail or operational observability work.
