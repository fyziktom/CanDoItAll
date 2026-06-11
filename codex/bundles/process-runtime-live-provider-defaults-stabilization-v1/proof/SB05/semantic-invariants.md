# SB05 Semantic Invariants

- Invariant ID: SB05_INV_001
- Source raw note: NOTE-001.
- Expected behavior: Build, full unit tests, focused deterministic process runtime tests, PostgreSQL business-plan automation, scheduler/workflow-origin paths, read-only verification jobs, and large desktop Playwright project-structure launch proof all pass.
- Disallowed shallow implementation: A status-only run, API-only UI claim, or screenshot without run readback is not enough.
- Failing-first test: N/A because no production behavior changed; SB05 is process/test/browser validation proof.
- Passing test: bundle://proof/SB05/transcripts/focused-integration-matrix.txt and bundle://proof/SB05/transcripts/playwright-project-structure-launch.txt.
- Changed source files: repo://tests/CanDoItAll.Tests.Integration/LiveProcessRunOpenAiSmokeIntegrationTests.cs with SHA-256 66D2BFDF089606BA1F852BDCEC6214E2796145A74A0B7992CC30490016B43128.
- Production assertions: repo://tests/CanDoItAll.Tests.Integration/ProcessTemplateExecutionE2ETests.cs, repo://tests/CanDoItAll.Tests.Integration/BusinessPlanProcessPostgresIntegrationTests.cs, and repo://tests/CanDoItAll.Tests.Playwright/AppSmokeTests.ProjectScopedProcessLaunch.cs.
- Red-team negative case: bundle://proof/SB08/transcripts/anti-stub-and-fake-proof-audit.txt rejects fake proof/stub closure.
- Downstream dependency check: SB06 and SB07 consume the deterministic/UI pass classification.

## Production Behavior Artifact Matrix
| artifact | producer | consumer | lifecycle | negative |
| --- | --- | --- | --- | --- |
| Existing deterministic process runtime proof | repo://tests/CanDoItAll.Tests.Integration/ProcessTemplateExecutionE2ETests.cs | bundle://proof/SB05/transcripts/focused-integration-matrix.txt | bundle://proof/SB05/transcripts/focused-integration-matrix.txt | bundle://proof/SB08/transcripts/anti-stub-and-fake-proof-audit.txt |
| Existing large desktop UI proof | repo://tests/CanDoItAll.Tests.Playwright/AppSmokeTests.ProjectScopedProcessLaunch.cs | bundle://proof/SB05/transcripts/playwright-project-structure-launch.txt | bundle://proof/SB05/screenshots/09-project-run-completed-steps-large-desktop.png | bundle://proof/SB05/transcripts/playwright-project-structure-launch.txt |
