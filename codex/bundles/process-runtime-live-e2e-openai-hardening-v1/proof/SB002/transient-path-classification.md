# SB002 Transient Bundle Path Classification

## Status
Completed.

## Scan
- Command transcript: `bundle://proof/SB002/transcripts/transient-path-classification-scan.txt`
- Scope: `repo://src` and `repo://tests`
- Result: 147 matches across 8 files.
- Product source matches: 0.
- C# test source matches: 0 direct concrete bundle-name matches.
- Fixture matches: 147, all under `repo://tests/CanDoItAll.Tests.Unit/TestData/Architecture`.

## Fixture Consumers
- `repo://tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs` reads `ProcessDriverMultiDomainVerificationGateway` fixtures and asserts SB003 proof references remain source-backed, not report-only.
- `repo://tests/CanDoItAll.Tests.Unit/ProcessDriverContractApiVerificationBoundaryTests.cs` reads `ProcessDriverMultiDomainVerificationGateway` fixtures for contract/API boundary snapshots.
- `repo://tests/CanDoItAll.Tests.Unit/ProcessDriverFakeProofResistanceTests.cs` reads `ProcessDriverMultiDomainVerificationGateway` proof fixtures and rejects status-only or fixture-only proof.
- `repo://tests/CanDoItAll.Tests.Unit/ProcessDriverContractPrerequisitesVerificationTests.cs` reads `ProcessDriverRuntimeEvidenceVerifierIntegrationHardening` fixtures for prerequisite and runtime-deferral invariants.
- `repo://tests/CanDoItAll.Tests.Unit/ProcessDriverTranscriptVerificationAlphaTests.cs` reads `ProcessDriverRuntimeEvidenceVerifierIntegrationHardening` fixtures for transcript-driver runtime-deferral invariants.

## Classification
| File | Matches | Classification | SB003 action |
| --- | ---: | --- | --- |
| `repo://tests/CanDoItAll.Tests.Unit/TestData/Architecture/ProcessDriverRuntimeEvidenceVerifierIntegrationHardening/reviews/01-execution-report.md` | 8 | Executed stable fixture content containing stale concrete bundle paths. | Normalize to portable `bundle://` or fixture-local references while preserving asserted report rows. |
| `repo://tests/CanDoItAll.Tests.Unit/TestData/Architecture/ProcessDriverRuntimeEvidenceVerifierIntegrationHardening/analysis/02-assumptions-and-risks.md` | 1 | Executed stable fixture content documenting a known risk. | Keep the generic risk language; remove concrete bundle name. |
| `repo://tests/CanDoItAll.Tests.Unit/TestData/Architecture/ProcessDriverMultiDomainVerificationGateway/proof/SB043/manifest.md` | 3 | Executed stable proof fixture consumed by fake-proof resistance tests. | Normalize proof links to `bundle://` or stable fixture references. |
| `repo://tests/CanDoItAll.Tests.Unit/TestData/Architecture/ProcessDriverMultiDomainVerificationGateway/proof/SB003/manifest.md` | 3 | Executed stable proof fixture consumed by Gate A tests. | Normalize proof links without weakening SB003 manifest assertions. |
| `repo://tests/CanDoItAll.Tests.Unit/TestData/Architecture/ProcessDriverMultiDomainVerificationGateway/proof/SB003/semantic-invariants.md` | 2 | Executed stable proof fixture consumed by Gate A tests. | Normalize proof links and retain invariant IDs. |
| `repo://tests/CanDoItAll.Tests.Unit/TestData/Architecture/ProcessDriverMultiDomainVerificationGateway/proof/SB003/transcripts/red-team-report-only-proof-rejection.txt` | 4 | Executed negative-proof transcript fixture. | Replace concrete bundle path text with portable bundle references. |
| `repo://tests/CanDoItAll.Tests.Unit/TestData/Architecture/ProcessDriverMultiDomainVerificationGateway/proof/SB003/transcripts/gate-a-proof-index.txt` | 9 | Executed proof-index transcript fixture. | Replace concrete bundle path text with portable bundle references. |
| `repo://tests/CanDoItAll.Tests.Unit/TestData/Architecture/ProcessDriverMultiDomainVerificationGateway/proof/SB003/transcripts/completed-validator-early-sb003-smoke.txt` | 120 | Executed validator-output transcript fixture. | Replace concrete bundle path text with portable bundle references or regenerated stable transcript content. |

## Proof
- Consumer tests: `bundle://proof/SB002/transcripts/focused-fixture-consumer-tests.txt`
- Test result: `bundle://proof/SB002/test-results/SB002-fixture-consumers.trx`
- Source assertion scan: `bundle://proof/SB002/transcripts/source-assertion-scan.txt`
- Anti-stub scan: `bundle://proof/SB002/transcripts/anti-stub-scan.txt`

## Closure
SB002 classifies every transient-path hit and proves the hits are long-lived unit-test fixture inputs. Cleanup is intentionally owned by the critical SB003 gate so the edits can be validated together with the no-transient-path scan, manifest, and semantic invariants.
