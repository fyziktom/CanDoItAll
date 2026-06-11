# SB05 Proof Manifest

## Status
- Result: Completed
- Scope: Deterministic runtime and UI regression matrix.

## Source Hashes
- repo://tests/CanDoItAll.Tests.Integration/LiveProcessRunOpenAiSmokeIntegrationTests.cs
  - SHA-256: 66D2BFDF089606BA1F852BDCEC6214E2796145A74A0B7992CC30490016B43128

## Evidence
- Passing solution build transcript: bundle://proof/SB05/transcripts/solution-build-no-restore.txt
- Passing full unit transcript: bundle://proof/SB05/transcripts/unit-tests.txt
- Passing focused deterministic integration transcript: bundle://proof/SB05/transcripts/focused-integration-matrix.txt
- Passing large desktop Playwright transcript: bundle://proof/SB05/transcripts/playwright-project-structure-launch.txt
- Passing screenshot artifacts: bundle://proof/SB05/screenshots/01-project-template-selected-large-desktop.png; bundle://proof/SB05/screenshots/09-project-run-completed-steps-large-desktop.png
- Anti-stub audit transcript: bundle://proof/SB08/transcripts/anti-stub-and-fake-proof-audit.txt
- Semantic invariant id transcript: bundle://proof/SB05/transcripts/semantic-invariant-id-index.txt
- Failing-first: N/A because no production behavior changed in SB05; this is process/test/browser validation proof.
- Semantic contract: bundle://proof/SB05/semantic-invariants.md

## Production Behavior Artifact Matrix
| artifact | producer | consumer | lifecycle | negative |
| --- | --- | --- | --- | --- |
| Existing deterministic process runtime proof | repo://tests/CanDoItAll.Tests.Integration/ProcessTemplateExecutionE2ETests.cs | bundle://proof/SB05/transcripts/focused-integration-matrix.txt | bundle://proof/SB05/transcripts/focused-integration-matrix.txt | bundle://proof/SB08/transcripts/anti-stub-and-fake-proof-audit.txt |
| Existing large desktop UI proof | repo://tests/CanDoItAll.Tests.Playwright/AppSmokeTests.ProjectScopedProcessLaunch.cs | bundle://proof/SB05/transcripts/playwright-project-structure-launch.txt | bundle://proof/SB05/screenshots/09-project-run-completed-steps-large-desktop.png | bundle://proof/SB05/transcripts/playwright-project-structure-launch.txt |
