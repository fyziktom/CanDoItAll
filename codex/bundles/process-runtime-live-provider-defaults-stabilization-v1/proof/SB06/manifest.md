# SB06 Proof Manifest

## Status
- Result: Completed
- Scope: Boundary and no-extraction scans.

## Source Hashes
- repo://tests/CanDoItAll.Tests.Integration/LiveProcessRunOpenAiSmokeIntegrationTests.cs
  - SHA-256: 66D2BFDF089606BA1F852BDCEC6214E2796145A74A0B7992CC30490016B43128

## Evidence
- Passing boundary/no-extraction transcript: bundle://proof/SB06/transcripts/boundary-no-extraction-scans.txt
- Passing provider-bypass transcript: bundle://proof/SB02/transcripts/provider-binding-source-assertions.txt
- Passing secret/fake-proof audit transcript: bundle://proof/SB08/transcripts/anti-stub-and-fake-proof-audit.txt
- Semantic invariant id transcript: bundle://proof/SB06/transcripts/semantic-invariant-id-index.txt
- Failing-first: N/A because no production behavior changed in SB06; this is process/source boundary validation proof.
- Semantic contract: bundle://proof/SB06/semantic-invariants.md

## Production Behavior Artifact Matrix
| artifact | producer | consumer | lifecycle | negative |
| --- | --- | --- | --- | --- |
| Existing process boundary constraints | repo://src/CanDoItAll.Processes.Core | bundle://proof/SB06/transcripts/boundary-no-extraction-scans.txt | bundle://proof/SB06/transcripts/boundary-no-extraction-scans.txt | bundle://proof/SB02/transcripts/provider-binding-source-assertions.txt |
