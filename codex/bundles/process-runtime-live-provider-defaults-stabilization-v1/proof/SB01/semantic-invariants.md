# SB01 Semantic Invariants

- Invariant ID: SB01_INV_001
- Source raw note: NOTE-001 and NOTE-005.
- Expected behavior: Current state is classified from prepared validation, source scans, deterministic proof, live proof, and boundary scans.
- Disallowed shallow implementation: A status-only statement or skipped live test cannot decide runtime stability.
- Failing-first test: N/A because no production behavior changed; SB01 is process/source classification proof.
- Passing test: bundle://proof/SB01/transcripts/prepared-stage-validator.txt and bundle://proof/SB04/transcripts/live-openai-process-smoke-summary.txt.
- Changed source files: repo://tests/CanDoItAll.Tests.Integration/LiveProcessRunOpenAiSmokeIntegrationTests.cs with SHA-256 66D2BFDF089606BA1F852BDCEC6214E2796145A74A0B7992CC30490016B43128.
- Production assertions: repo://src/CanDoItAll.Modules.Processes plus bundle://proof/SB06/transcripts/boundary-no-extraction-scans.txt.
- Red-team negative case: bundle://proof/SB08/transcripts/anti-stub-and-fake-proof-audit.txt rejects status-only or fake proof closure.
- Downstream dependency check: SB02 through SB08 use this classification and cite bundle://proof/SB01/manifest.md.

## Production Behavior Artifact Matrix
| artifact | producer | consumer | lifecycle | negative |
| --- | --- | --- | --- | --- |
| Existing process runtime classification | repo://tests/CanDoItAll.Tests.Integration/LiveProcessRunOpenAiSmokeIntegrationTests.cs | bundle://proof/SB04/transcripts/live-openai-process-smoke-summary.txt | bundle://proof/SB05/transcripts/focused-integration-matrix.txt | bundle://proof/SB06/transcripts/boundary-no-extraction-scans.txt |
