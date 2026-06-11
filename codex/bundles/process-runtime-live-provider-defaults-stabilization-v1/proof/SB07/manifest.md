# SB07 Proof Manifest

## Status
- Result: Completed
- Scope: Final release decision.

## Source Hashes
- repo://tests/CanDoItAll.Tests.Integration/LiveProcessRunOpenAiSmokeIntegrationTests.cs
  - SHA-256: 66D2BFDF089606BA1F852BDCEC6214E2796145A74A0B7992CC30490016B43128

## Evidence
- Release decision artifact: bundle://proof/SB07/release-decision.md
- Passing live transcript: bundle://proof/SB04/transcripts/live-openai-process-smoke-summary.txt
- Passing deterministic/UI transcript: bundle://proof/SB05/transcripts/focused-integration-matrix.txt
- Passing boundary transcript: bundle://proof/SB06/transcripts/boundary-no-extraction-scans.txt
- Passing build transcript: bundle://proof/SB05/transcripts/solution-build-no-restore.txt
- Anti-stub audit transcript: bundle://proof/SB08/transcripts/anti-stub-and-fake-proof-audit.txt
- Semantic invariant id transcript: bundle://proof/SB07/transcripts/semantic-invariant-id-index.txt
- Failing-first: N/A because no production behavior changed in SB07; this is process/release classification proof.
- Semantic contract: bundle://proof/SB07/semantic-invariants.md

## Production Behavior Artifact Matrix
| artifact | producer | consumer | lifecycle | negative |
| --- | --- | --- | --- | --- |
| Existing release classification | bundle://proof/SB07/release-decision.md | bundle://proof/SB04/transcripts/live-openai-process-smoke-summary.txt | bundle://proof/SB05/transcripts/focused-integration-matrix.txt | bundle://proof/SB06/transcripts/boundary-no-extraction-scans.txt |
