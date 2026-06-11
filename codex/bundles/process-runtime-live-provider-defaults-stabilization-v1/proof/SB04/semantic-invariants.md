# SB04 Semantic Invariants

- Invariant ID: SB04_INV_001
- Source raw note: NOTE-002 and NOTE-004.
- Expected behavior: The opt-in live process-run smoke executes through the managed OpenAI default provider with no explicit model override and completes without skipped live proof.
- Disallowed shallow implementation: A skipped live test, workspace-only agent call, or direct provider call is not live process-run proof.
- Failing-first test: N/A because no production behavior changed; SB04 is process/live validation proof.
- Passing test: bundle://proof/SB04/transcripts/live-openai-process-smoke-summary.txt.
- Changed source files: repo://tests/CanDoItAll.Tests.Integration/LiveProcessRunOpenAiSmokeIntegrationTests.cs with SHA-256 66D2BFDF089606BA1F852BDCEC6214E2796145A74A0B7992CC30490016B43128.
- Production assertions: repo://tests/CanDoItAll.Tests.Integration/LiveProcessRunOpenAiSmokeIntegrationTests.cs and bundle://proof/SB02/transcripts/provider-binding-source-assertions.txt.
- Red-team negative case: bundle://proof/SB02/transcripts/provider-binding-source-assertions.txt rejects direct OpenAI/raw HTTP bypass in scoped paths.
- Downstream dependency check: SB05 and SB07 consume the live-passed classification.

## Production Behavior Artifact Matrix
| artifact | producer | consumer | lifecycle | negative |
| --- | --- | --- | --- | --- |
| Existing live process-run provider execution | repo://tests/CanDoItAll.Tests.Integration/LiveProcessRunOpenAiSmokeIntegrationTests.cs | bundle://proof/SB04/transcripts/live-openai-process-smoke-summary.txt | bundle://proof/SB04/transcripts/live-openai-process-smoke-summary.txt | bundle://proof/SB02/transcripts/provider-binding-source-assertions.txt |
