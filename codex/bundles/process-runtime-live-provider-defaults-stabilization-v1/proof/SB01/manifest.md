# SB01 Proof Manifest

## Status
- Result: Completed
- Scope: Current state and blocker taxonomy.

## Source Hashes
- repo://tests/CanDoItAll.Tests.Integration/LiveProcessRunOpenAiSmokeIntegrationTests.cs
  - SHA-256: 66D2BFDF089606BA1F852BDCEC6214E2796145A74A0B7992CC30490016B43128

## Evidence
- Passing prepared gate transcript: bundle://proof/SB01/transcripts/prepared-stage-validator.txt
- Passing taxonomy/source transcript: bundle://proof/SB02/transcripts/provider-binding-source-assertions.txt
- Passing boundary transcript: bundle://proof/SB06/transcripts/boundary-no-extraction-scans.txt
- Passing live classification transcript: bundle://proof/SB04/transcripts/live-openai-process-smoke-summary.txt
- Anti-stub audit transcript: bundle://proof/SB08/transcripts/anti-stub-and-fake-proof-audit.txt
- Semantic invariant id transcript: bundle://proof/SB01/transcripts/semantic-invariant-id-index.txt
- Failing-first: N/A because no production behavior was changed in SB01; this is process/source classification proof.
- Semantic contract: bundle://proof/SB01/semantic-invariants.md

## Production Behavior Artifact Matrix
| artifact | producer | consumer | lifecycle | negative |
| --- | --- | --- | --- | --- |
| Existing process runtime classification | repo://tests/CanDoItAll.Tests.Integration/LiveProcessRunOpenAiSmokeIntegrationTests.cs | bundle://proof/SB04/transcripts/live-openai-process-smoke-summary.txt | bundle://proof/SB05/transcripts/focused-integration-matrix.txt | bundle://proof/SB06/transcripts/boundary-no-extraction-scans.txt |
