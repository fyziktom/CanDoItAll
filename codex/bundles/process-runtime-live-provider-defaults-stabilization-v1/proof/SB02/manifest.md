# SB02 Proof Manifest

## Status
- Result: Completed
- Scope: Provider binding audit.

## Source Hashes
- repo://tests/CanDoItAll.Tests.Integration/LiveProcessRunOpenAiSmokeIntegrationTests.cs
  - SHA-256: 66D2BFDF089606BA1F852BDCEC6214E2796145A74A0B7992CC30490016B43128

## Evidence
- Passing provider binding transcript: bundle://proof/SB02/transcripts/provider-binding-source-assertions.txt
- Passing live managed-provider transcript: bundle://proof/SB04/transcripts/live-openai-process-smoke-summary.txt
- Passing secret/fake-proof audit transcript: bundle://proof/SB08/transcripts/anti-stub-and-fake-proof-audit.txt
- Semantic invariant id transcript: bundle://proof/SB02/transcripts/semantic-invariant-id-index.txt
- Failing-first: N/A because no production behavior changed in SB02; this is process/source audit proof.
- Semantic contract: bundle://proof/SB02/semantic-invariants.md

## Production Behavior Artifact Matrix
| artifact | producer | consumer | lifecycle | negative |
| --- | --- | --- | --- | --- |
| Existing managed provider binding | repo://tests/CanDoItAll.Tests.Integration/LiveProcessRunOpenAiSmokeIntegrationTests.cs | bundle://proof/SB04/transcripts/live-openai-process-smoke-summary.txt | bundle://proof/SB02/transcripts/provider-binding-source-assertions.txt | bundle://proof/SB02/transcripts/provider-binding-source-assertions.txt |
