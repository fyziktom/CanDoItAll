# SB006 Semantic Invariants

## Invariant SB006-INV-001
- Invariant ID: `SB006-INV-001`
- Source raw note: "Podivej se jak dopadl realny test" and the bundle requirement to classify the current live OpenAI proof accurately.
- Expected behavior: The bundle must classify the existing live OpenAI evidence as a guarded workspace specialist-agent smoke and must not claim live process-run proof until a Process module run path is exercised.
- Disallowed shallow implementation: Marking the live proof as process-run proof because an OpenAI-backed integration test passed, or treating a skipped live test as functional proof.
- Failing-first test: `bundle://proof/SB006/transcripts/red-team-live-skip-as-process-run-rejection.txt` rejects the shallow claim and records `ExitCode: 1`.
- Passing test: `bundle://proof/SB006/transcripts/gate-b-proof-index.txt` verifies classification, source scan, skip-path validation, and red-team artifacts with `ExitCode: 0`.
- Changed source files: `repo://tests/CanDoItAll.Tests.Integration/LiveSpecialistAgentScenarioIntegrationTests.cs` hash `fec545c964d7fadcf9c85919781c6e030a4ff002bd66102669c330b86a087bcf`.
- Production assertions: `bundle://proof/SB004/transcripts/specialist-agent-live-proof-classification.txt` proves workspace-agent markers and absence of process-run markers; `bundle://proof/SB005/transcripts/live-env-gate-source-assertions.txt` proves the two-flag opt-in gate and bounded timeout.
- Red-team negative case: `bundle://proof/SB006/transcripts/red-team-live-skip-as-process-run-rejection.txt`.
- Downstream dependency check: SB007-SB009 must add separate opt-in process-run proof or explicitly classify it as skipped/blocked; they cannot reuse the specialist-agent transcript as process-run proof.

## Production Behavior Artifact Matrix
| Artifact | Producer proof | Consumer proof | Lifecycle proof | Negative proof |
| --- | --- | --- | --- | --- |
| `LiveSpecialistSmokeTwoFlagGate` | `repo://tests/CanDoItAll.Tests.Integration/LiveSpecialistAgentScenarioIntegrationTests.cs` | `bundle://proof/SB005/transcripts/live-specialist-smoke-two-flag-skip-test.txt` | Test-only lifecycle; no production runtime lifecycle added. | `bundle://proof/SB006/transcripts/red-team-live-skip-as-process-run-rejection.txt` |
