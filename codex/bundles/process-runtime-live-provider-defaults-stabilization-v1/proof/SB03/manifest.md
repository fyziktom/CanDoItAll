# SB03 Proof Manifest

## Status
- Result: Completed
- Scope: Managed provider default model policy.

## Source Hashes
- repo://tests/CanDoItAll.Tests.Integration/LiveProcessRunOpenAiSmokeIntegrationTests.cs
  - SHA-256: 66D2BFDF089606BA1F852BDCEC6214E2796145A74A0B7992CC30490016B43128

## Evidence
- Passing focused model policy tests: bundle://proof/SB03/transcripts/focused-model-policy-tests.txt
- Passing source assertions and hash transcript: bundle://proof/SB03/transcripts/model-policy-source-assertions-and-hashes.txt
- Adversarial negative proof transcript: bundle://proof/SB03/transcripts/adversarial-old-required-model-policy-absent.txt
- Passing live provider-default proof: bundle://proof/SB04/transcripts/live-openai-process-smoke-summary.txt
- Anti-stub audit transcript: bundle://proof/SB08/transcripts/anti-stub-and-fake-proof-audit.txt
- Semantic invariant id transcript: bundle://proof/SB03/transcripts/semantic-invariant-id-index.txt
- Semantic contract: bundle://proof/SB03/semantic-invariants.md

## Production Behavior Artifact Matrix
| artifact | producer | consumer | lifecycle | negative |
| --- | --- | --- | --- | --- |
| Existing live smoke model resolution | repo://tests/CanDoItAll.Tests.Integration/LiveProcessRunOpenAiSmokeIntegrationTests.cs | bundle://proof/SB04/transcripts/live-openai-process-smoke-summary.txt | bundle://proof/SB03/transcripts/focused-model-policy-tests.txt | bundle://proof/SB03/transcripts/adversarial-old-required-model-policy-absent.txt |
