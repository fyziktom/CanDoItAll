# SB05 Semantic Invariants

- Invariant ID: `SB05_INV_001`
- Source raw note: Restore reliable business-analysis template process execution.
- Expected behavior: `business-plan-development` completes through automation dispatch with process-mock role assignments, finalizer summaries, completed step readback, and business-plan artifacts recorded.
- Disallowed shallow implementation: manually transitioning business-plan steps or asserting only that the template imports.
- Failing-first test: `bundle://proof/SB05/transcripts/failing-first-source-assertion.txt` shows the baseline lacked the SB05 automation E2E.
- Passing test: `bundle://proof/SB05/transcripts/focused-test.txt` shows the focused SB05 E2E passed.
- Changed source files: `repo://tests/CanDoItAll.Tests.Integration/BusinessPlanProcessPostgresIntegrationTests.cs`, `repo://tests/CanDoItAll.Tests.Integration/ProcessTemplateAutomationTestSupport.cs`, `repo://src/CanDoItAll.Modules.AgentFramework/Hosting/ProcessMockAgentRuntime.cs`.
- Production assertions: the shared process mock resolves prompt-required artifacts and tool receipts generically; SB05 does not use .NET/browser-only assertions as its proof.
- Red-team negative case: a Blazor-only harness would fail this test because the business-plan template has different roles, step titles, and artifact expectations.
- Downstream dependency check: SB06 may proceed because runtime-host readback can now build on real automation-dispatched template runs.
