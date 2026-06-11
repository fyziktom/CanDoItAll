# SB03 Semantic Invariants

- Invariant ID: `SB03_INV_001`
- Source raw note: Restore reliable Blazor/.NET template execution without manual transition-only proof.
- Expected behavior: `blazor-app-delivery` completes through automation dispatch with process-mock agents, finalizer summaries, branch readback, and required artifacts recorded.
- Disallowed shallow implementation: manually transitioning steps, suppressing automation dispatch, or asserting only that the definition imports.
- Failing-first test: `bundle://proof/SB03/transcripts/failing-first-source-assertion.txt` shows the baseline lacked the SB03 automation E2E test and shared process-mock template harness.
- Passing test: `bundle://proof/SB03/transcripts/focused-test.txt` shows the focused SB03 E2E passed.
- Changed source files: `repo://tests/CanDoItAll.Tests.Integration/ProcessTemplateExecutionE2ETests.cs`, `repo://tests/CanDoItAll.Tests.Integration/ProcessTemplateAutomationTestSupport.cs`, `repo://src/CanDoItAll.Modules.AgentFramework/Hosting/ProcessMockAgentRuntime.cs`, `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ToolValidation.cs`.
- Production assertions: `bundle://proof/SB03/transcripts/source-assertions.txt` shows the process-mock harness, finalizer assertions, prompt-required artifacts, and required tool receipts.
- Red-team negative case: a no-op import-only Blazor test would fail the source assertion because it lacks `ExecuteTemplateWithProcessMockAgentsAsync` and `AssertFinalizerSummaries`.
- Downstream dependency check: SB04 may proceed because the Blazor template host path now proves dispatch/finalizer/readback behavior.
