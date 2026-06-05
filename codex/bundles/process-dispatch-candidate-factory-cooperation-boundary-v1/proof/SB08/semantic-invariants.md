# SB08 Semantic Invariants

- Invariant ID: SB08-INV-001
- Source raw note: Preserve original candidate construction behavior while moving subprocess and workflow candidates behind a factory.
- Expected behavior: The dispatcher has no direct DispatchCandidate constructor call and subprocess/workflow candidates preserve common fields, empty technical agent id, empty recovery fields, branch facts, and read-only handoff cooperation metadata.
- Disallowed shallow implementation: Only moving one route or dropping branch/artifact/common fields while tests assert only construction succeeds.
- Failing-first test: bundle://proof/SB04/transcripts/sb04-failing-first-candidate-factory-guardrail.txt
- Passing test: bundle://proof/SB08/transcripts/sb08-candidate-factory-route-parity-tests.txt; bundle://proof/SB08/transcripts/sb08-integration-route-parity-tests.txt
- Changed source files: repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchCandidateFactory.cs; repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Dispatch.cs; repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs
- Production assertions: Subprocess and workflow candidates are created only by ProcessDispatchCandidateFactory and copy all common assembly-context fields.
- Red-team negative case: SB08 source assertion transcript rejects direct dispatcher constructor ownership and fixture-only route checks.
- Downstream dependency check: SB12 direct-agent parity reuses the same assembly context and constructor owner.

- Invariant ID: SB08-INV-002
- Source raw note: Preserve candidate header selection and hydration readback behavior.
- Expected behavior: Existing hydration boundary tests still pass while route construction moves to the factory.
- Disallowed shallow implementation: Factory extraction that bypasses ProcessDispatchCandidateHydrationLoader or branch/artifact input assembly.
- Failing-first test: bundle://proof/SB04/transcripts/sb04-failing-first-candidate-factory-guardrail.txt
- Passing test: bundle://proof/SB01/transcripts/sb01-focused-dispatch-boundary-tests.txt; bundle://proof/SB08/transcripts/sb08-integration-route-parity-tests.txt
- Changed source files: repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Dispatch.cs
- Production assertions: Hydration loader remains the only candidate snapshot loader before assembly context creation.
- Red-team negative case: Source assertion transcript checks no dispatcher constructor call remains.
- Downstream dependency check: SB16 source scan confirms single constructor owner after all route movement.
