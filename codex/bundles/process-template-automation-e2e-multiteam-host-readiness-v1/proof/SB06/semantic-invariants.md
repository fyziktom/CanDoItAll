# SB06 Semantic Invariants

- Invariant ID: `SB06_INV_001`
- Source raw note: Continue toward generic process-driver runtime host.
- Expected behavior: A real template run supplies `ProcessRunId` and completed `StepRunId`; manager readback returns capability key, audit id/hash, evidence count, no denial, and no-mutation flags; dry-run readback returns denied execution, audit reference, authorization gaps, and no-mutation flags.
- Disallowed shallow implementation: using hand-created ids, DTO-only readback, or any execution-capable mutation as proof.
- Failing-first test: `bundle://proof/SB06/transcripts/failing-first-source-assertion.txt` shows the baseline lacked the SB06 real-run runtime-host test.
- Passing test: `bundle://proof/SB06/transcripts/focused-test.txt` shows the focused SB06 test passed.
- Changed source files: `repo://tests/CanDoItAll.Tests.Integration/ProcessDomainEvidenceReadOnlyAdapterTests.cs`, `repo://tests/CanDoItAll.Tests.Integration/ProcessTemplateAutomationTestSupport.cs`, `repo://src/CanDoItAll.Modules.AgentFramework/Hosting/ProcessMockAgentRuntime.cs`.
- Production assertions: `ProcessManagerReadOnlyVerificationReadback`, `ProcessManagerRuntimeHostDryRunReadback`, and `ProcessVerificationRuntimeHostStatus` remain read-only surfaces.
- Red-team negative case: an execution-capable command path remains denied by `ProcessExecutionCapableDriverFutureGate`.
- Downstream dependency check: SB07 can build on scheduler/workflow jobs that route through the same manager-host readback boundary.
