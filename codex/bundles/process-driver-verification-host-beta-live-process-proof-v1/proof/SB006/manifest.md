# SB006 Proof Manifest

Status: `Completed`

## Owned Scope
- Subbundle: `SB006 - Critical Gate B live-proof classification`
- Requirements: `REQ-003`
- Raw note: "Look at real test outcome" and "Fix live OpenAI/provider proof gap."
- Semantic invariant contract: `bundle://proof/SB006/semantic-invariants.md`

## Changed File Hashes
| File | SHA-256 |
| --- | --- |
| `repo://tests/CanDoItAll.Tests.Integration/LiveSpecialistAgentScenarioIntegrationTests.cs` | `fec545c964d7fadcf9c85919781c6e030a4ff002bd66102669c330b86a087bcf` |
| `bundle://subbundles/SB004/README.md` | `c9d725cef1ac76e2791e0f67eb729e52afff9c379436024b14a4422ba9161036` |
| `bundle://subbundles/SB005/README.md` | `317c19061d199150657270bc1f9bb6c7e712609fd67689b2ffe5ee6b223add1f` |
| `bundle://subbundles/SB006/README.md` | `9e8b95705c8ae05651387e3357320b29566a0345d52413b2441cce1ffa3b18b6` |
| `bundle://reviews/01-execution-report.md` | `0754397c7f9f52604e31bee1754d054f9da7e23bd96d846296db3ddf553bd514` |
| `bundle://proof/SB006/semantic-invariants.md` | `36ea34cfd9a495ac9349df3cf7c4fa351ee1c63b4783aca8baea0cbf963e47fd` |
| `bundle://proof/SB004/transcripts/specialist-agent-live-proof-classification.txt` | `11a88a2da5b56caeaa1b9bf4b99c6fae292341344a1f7c8fc646000d439d4b67` |
| `bundle://proof/SB005/transcripts/live-env-gate-source-assertions.txt` | `1d46eae908318a93143a70865aa22cb8d16ceac26d4d98423e15d2b873daa89c` |
| `bundle://proof/SB005/transcripts/live-specialist-smoke-two-flag-skip-test.txt` | `d63b7683490ea70384ec457c08354adf756bad5187b4e409fa742deb9a09e875` |
| `bundle://proof/SB006/transcripts/live-proof-classification-source-scan.txt` | `957de1b7973bf34f0fa9933f92709bc98f3c10e212477720f34b3b85b8715755` |
| `bundle://proof/SB006/transcripts/red-team-live-skip-as-process-run-rejection.txt` | `07f48aebdcc68727138b9febd3b89302590930a57e4bc6db4bbe74b7fc9fadec` |
| `bundle://proof/SB006/transcripts/gate-b-proof-index.txt` | `0886e45ed064b16cfef0d6f1b233adee5e22c31cf23fcdbb1de2202a28d800df` |

## Command Transcripts
- Specialist-agent classification: `bundle://proof/SB004/transcripts/specialist-agent-live-proof-classification.txt`
- Live env gate source assertions: `bundle://proof/SB005/transcripts/live-env-gate-source-assertions.txt`
- Live specialist smoke skip-path test: `bundle://proof/SB005/transcripts/live-specialist-smoke-two-flag-skip-test.txt`
- Source classification scan: `bundle://proof/SB006/transcripts/live-proof-classification-source-scan.txt`
- Adversarial negative proof: `bundle://proof/SB006/transcripts/red-team-live-skip-as-process-run-rejection.txt`
- Passing proof: `bundle://proof/SB006/transcripts/gate-b-proof-index.txt`

## Source Assertions
- `repo://tests/CanDoItAll.Tests.Integration/LiveSpecialistAgentScenarioIntegrationTests.cs` exercises workspace specialist agents through `ICanDoItAllAgentWorkspaceFactory`, `GetOrganizationWorkspaceService`, and `SendMessageAsync`.
- The same test source does not reference `ProcessesService`, `ProcessRunAutomationDispatchService`, or `ProcessWorkflowRunCoordinator`; therefore the existing live proof is not a process-run proof.
- The live specialist smoke now requires both `CANDOITALL_RUN_LIVE_AGENT_VALIDATION` and `CANDOITALL_ENABLE_LIVE_OPENAI_SMOKE` before it can reach the `OPENAI_API_KEY` assertion.
- The test checks secret presence only and retains a bounded four-minute cancellation token.

## Failing-First And Passing Proof
- Failing-first/adversarial negative proof: `bundle://proof/SB006/transcripts/red-team-live-skip-as-process-run-rejection.txt` records `ExitCode: 1` for treating a specialist-agent smoke or skip-path pass as live process-run proof.
- Passing proof: `bundle://proof/SB006/transcripts/gate-b-proof-index.txt` records `ExitCode: 0` after classification, source assertions, skip-path validation, and red-team evidence exist.

## Anti-Stub Audit
- Source classification scan: `bundle://proof/SB006/transcripts/live-proof-classification-source-scan.txt`
- Supporting production anti-stub audit: `bundle://proof/SB003/transcripts/source-scan-and-anti-stub-audit.txt`

## Production Behavior Artifact Matrix
| Artifact | Producer proof | Consumer proof | Lifecycle proof | Negative proof |
| --- | --- | --- | --- | --- |
| `LiveSpecialistSmokeTwoFlagGate` | `repo://tests/CanDoItAll.Tests.Integration/LiveSpecialistAgentScenarioIntegrationTests.cs` plus `bundle://proof/SB005/transcripts/live-env-gate-source-assertions.txt` | `bundle://proof/SB005/transcripts/live-specialist-smoke-two-flag-skip-test.txt` proves the test consumes the two-flag gate before reaching live work. | Test lifecycle only; no production scheduler or runtime lifecycle is introduced by this gate. | `bundle://proof/SB006/transcripts/red-team-live-skip-as-process-run-rejection.txt` rejects counting skipped or specialist-agent proof as process-run proof. |

## Downstream Dependency Check
- SB007-SB009 may add live process-run smoke proof only after preserving this classification: current live evidence is specialist-agent proof, not process-run proof.
- Reopen SB006 if a downstream report marks a skipped live test as pass evidence or claims process-run proof without process-run dispatch, finalizer, artifacts, and provider usage observations.
