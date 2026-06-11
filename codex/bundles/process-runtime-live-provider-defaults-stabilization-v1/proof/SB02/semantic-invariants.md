# SB02 Semantic Invariants

- Invariant ID: SB02_INV_001
- Source raw note: NOTE-002.
- Expected behavior: Live process-run proof uses the CanDoItAll managed OpenAI provider through workspace service, provider profile binding, MAF/process dispatch, execution-run readback, and usage observations.
- Disallowed shallow implementation: A direct OpenAI client call, raw HTTP call, or provider name string alone is not enough.
- Failing-first test: N/A because no production behavior changed; SB02 is process/source audit proof.
- Passing test: bundle://proof/SB02/transcripts/provider-binding-source-assertions.txt and bundle://proof/SB04/transcripts/live-openai-process-smoke-summary.txt.
- Changed source files: repo://tests/CanDoItAll.Tests.Integration/LiveProcessRunOpenAiSmokeIntegrationTests.cs with SHA-256 66D2BFDF089606BA1F852BDCEC6214E2796145A74A0B7992CC30490016B43128.
- Production assertions: repo://src/CanDoItAll.Modules.Processes plus bundle://proof/SB02/transcripts/provider-binding-source-assertions.txt.
- Red-team negative case: bundle://proof/SB02/transcripts/provider-binding-source-assertions.txt reports no direct OpenAI client/raw HTTP bypass tokens in scoped process paths.
- Downstream dependency check: SB03 and SB04 use the managed provider route proven here.

## Production Behavior Artifact Matrix
| artifact | producer | consumer | lifecycle | negative |
| --- | --- | --- | --- | --- |
| Existing managed provider binding | repo://tests/CanDoItAll.Tests.Integration/LiveProcessRunOpenAiSmokeIntegrationTests.cs | bundle://proof/SB04/transcripts/live-openai-process-smoke-summary.txt | bundle://proof/SB02/transcripts/provider-binding-source-assertions.txt | bundle://proof/SB02/transcripts/provider-binding-source-assertions.txt |
