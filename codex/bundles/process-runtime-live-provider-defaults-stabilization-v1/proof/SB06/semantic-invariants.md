# SB06 Semantic Invariants

- Invariant ID: SB06_INV_001
- Source raw note: NOTE-005.
- Expected behavior: No Process Runtime Core extraction, dispatcher/outbox/finalizer move, execution-capable driver hook, fallback selector, reflection discovery, scheduler/workflow direct driver hook, or secret leakage is introduced.
- Disallowed shallow implementation: Checking project names only, or ignoring scoped source files, cannot prove the boundary.
- Failing-first test: N/A because no production behavior changed; SB06 is process/source boundary validation proof.
- Passing test: bundle://proof/SB06/transcripts/boundary-no-extraction-scans.txt.
- Changed source files: repo://tests/CanDoItAll.Tests.Integration/LiveProcessRunOpenAiSmokeIntegrationTests.cs with SHA-256 66D2BFDF089606BA1F852BDCEC6214E2796145A74A0B7992CC30490016B43128.
- Production assertions: repo://src/CanDoItAll.Processes.Core, repo://src/CanDoItAll.Processes.Contracts, repo://src/CanDoItAll.Modules.Processes, and bundle://proof/SB06/transcripts/boundary-no-extraction-scans.txt.
- Red-team negative case: bundle://proof/SB06/transcripts/boundary-no-extraction-scans.txt checks direct driver hooks, runtime-core paths, fallback/reflection tokens, and secret-shaped values.
- Downstream dependency check: SB07 release decision consumes this boundary pass.

## Production Behavior Artifact Matrix
| artifact | producer | consumer | lifecycle | negative |
| --- | --- | --- | --- | --- |
| Existing process boundary constraints | repo://src/CanDoItAll.Processes.Core | bundle://proof/SB06/transcripts/boundary-no-extraction-scans.txt | bundle://proof/SB06/transcripts/boundary-no-extraction-scans.txt | bundle://proof/SB02/transcripts/provider-binding-source-assertions.txt |
