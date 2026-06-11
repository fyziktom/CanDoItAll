# SB07 Semantic Invariants

- Invariant ID: SB07_INV_001
- Source raw note: NOTE-001 through NOTE-005.
- Expected behavior: The final classification is `runtime-stable-live-passed` because build, unit, deterministic integration, Playwright, live OpenAI, provider binding, and boundary proof all passed.
- Disallowed shallow implementation: Advisory code/proof ratio text, skipped live proof, or deterministic-only proof cannot claim live runtime stability.
- Failing-first test: N/A because no production behavior changed; SB07 is process/release classification proof.
- Passing test: bundle://proof/SB07/release-decision.md, bundle://proof/SB04/transcripts/live-openai-process-smoke-summary.txt, and bundle://proof/SB05/transcripts/focused-integration-matrix.txt.
- Changed source files: repo://tests/CanDoItAll.Tests.Integration/LiveProcessRunOpenAiSmokeIntegrationTests.cs with SHA-256 66D2BFDF089606BA1F852BDCEC6214E2796145A74A0B7992CC30490016B43128.
- Production assertions: repo://src/CanDoItAll.Modules.Processes and bundle://proof/SB06/transcripts/boundary-no-extraction-scans.txt.
- Red-team negative case: bundle://proof/SB08/transcripts/anti-stub-and-fake-proof-audit.txt rejects fake proof/stub release closure.
- Downstream dependency check: SB08 stabilization ledger uses the release decision and keeps extraction frozen.

## Production Behavior Artifact Matrix
| artifact | producer | consumer | lifecycle | negative |
| --- | --- | --- | --- | --- |
| Existing release classification | bundle://proof/SB07/release-decision.md | bundle://proof/SB04/transcripts/live-openai-process-smoke-summary.txt | bundle://proof/SB05/transcripts/focused-integration-matrix.txt | bundle://proof/SB06/transcripts/boundary-no-extraction-scans.txt |
