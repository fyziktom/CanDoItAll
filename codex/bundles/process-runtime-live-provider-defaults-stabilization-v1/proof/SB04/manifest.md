# SB04 Proof Manifest

## Status
- Result: Completed
- Scope: Live OpenAI process-run smoke rerun.

## Source Hashes
- repo://tests/CanDoItAll.Tests.Integration/LiveProcessRunOpenAiSmokeIntegrationTests.cs
  - SHA-256: 66D2BFDF089606BA1F852BDCEC6214E2796145A74A0B7992CC30490016B43128

## Evidence
- Passing live smoke transcript: bundle://proof/SB04/transcripts/live-openai-process-smoke-summary.txt
- Passing provider path transcript: bundle://proof/SB02/transcripts/provider-binding-source-assertions.txt
- Anti-stub audit transcript: bundle://proof/SB08/transcripts/anti-stub-and-fake-proof-audit.txt
- Semantic invariant id transcript: bundle://proof/SB04/transcripts/semantic-invariant-id-index.txt
- Failing-first: N/A because no production behavior changed in SB04; this is process/live validation proof.
- Semantic contract: bundle://proof/SB04/semantic-invariants.md

## Production Behavior Artifact Matrix
| artifact | producer | consumer | lifecycle | negative |
| --- | --- | --- | --- | --- |
| Existing live process-run provider execution | repo://tests/CanDoItAll.Tests.Integration/LiveProcessRunOpenAiSmokeIntegrationTests.cs | bundle://proof/SB04/transcripts/live-openai-process-smoke-summary.txt | bundle://proof/SB04/transcripts/live-openai-process-smoke-summary.txt | bundle://proof/SB02/transcripts/provider-binding-source-assertions.txt |
