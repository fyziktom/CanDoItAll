# SB014 Semantic Invariants

## Invariants

- Invariant ID: `SB014-INV-001`
- Source raw note: `Keep facts/fingerprint/directive pure, journal/rerun application-local.`
- Expected behavior: Missing upstream artifact materialization computes missing inputs, runnable target, block reason, fingerprint, and rerun directive without persistence or service scopes; journal dedupe and rerun request execution remain in application side-effect coordinators.
- Disallowed shallow implementation: Moving functions into separate files while pure materialization code still creates EF contexts, writes journals, resolves services, or calls rerun APIs.
- Failing-first test: `N/A - no production behavior change was intended; this subbundle validates an existing behavior-preserving materialization ownership split.`
- Passing test: `bundle://proof/SB014/transcripts/upstream-materialization-architecture-test.txt` and `bundle://proof/SB014/transcripts/upstream-materialization-focused-integration-tests.txt`
- Changed source files: `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessMissingUpstreamArtifactMaterialization.cs`, `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessMissingUpstreamArtifactMaterializationSideEffects.cs`, `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchPreExecutionGuardHandler.cs`, `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchRouteServices.cs`, `repo://tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs`, `repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs`
- Production assertions: `bundle://proof/SB014/transcripts/upstream-materialization-source-assertions.txt`
- Red-team negative case: Adding `CreateDbContextAsync`, `SaveChangesAsync`, `CreateAsyncScope`, or `RerunAgentStepAsync` to `ProcessMissingUpstreamArtifactMaterialization.cs` fails the SB014 architecture/source assertions.
- Downstream dependency check: `SB015` may run pre-execution parity because database blocking and upstream materialization ownership are proved.

## Raw Note Closure

- Upstream materialization split: `Solved for SB014 with pure facts/fingerprint/directive and application-local journal/rerun side effects.`
- Preserve behavior: `Proved through focused facts, fingerprint, and rerun request tests.`
- Do not rush Process Core: `Partially solved without creating Core; final decision remains owned by SB036.`
