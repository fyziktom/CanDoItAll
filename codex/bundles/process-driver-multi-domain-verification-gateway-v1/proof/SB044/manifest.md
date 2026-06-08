# SB044 Proof Manifest

## Status
- Subbundle: `SB044`
- Status: `Completed`
- Owned requirement: `REQ-013`
- Scope result: Focused red-team tests now reject fake proof based on status-only reports, non-empty diagnostics, unredacted fixture-secret leakage, and fixture-only parsing.

## Changed File Hashes
| File | SHA-256 |
| --- | --- |
| `repo://tests/CanDoItAll.Tests.Unit/ProcessDriverFakeProofResistanceTests.cs` | `86aa39949c780dee594d24af5eb8e0fda121ab86a1f93ccf9c4b5c47497c9500` |
| `repo://codex/bundles/process-driver-multi-domain-verification-gateway-v1/subbundles/sb044-add-red-team-tests-for-fake-proof-non-empty-diagnostics-status-only-re/README.md` | `66c88a1d64ce8bbb0fb13db1c20121378f328d8a79dde328c0ad1bd6844912f6` |
| `repo://codex/bundles/process-driver-multi-domain-verification-gateway-v1/README.md` | `30101d8b1ebac484f4191919b9b5ec9ae7ac8ce9b626d5d357e59746a1307079` |
| `repo://codex/bundles/process-driver-multi-domain-verification-gateway-v1/reviews/01-execution-report.md` | `2e361a1bb4a9d96f1999de7a7ba21f10dd2d0da9fb60ac7876ce913e113ac259` |

## Command Transcripts
- Focused fake-proof resistance tests: `bundle://proof/SB044/transcripts/focused-fake-proof-resistance-tests.txt`
- Fake-proof resistance source scan and anti-stub audit: `bundle://proof/SB044/transcripts/fake-proof-resistance-source-scan-and-anti-stub-audit.txt`

## Source Assertions
- `ProcessDriverFakeProofResistanceTests` models shallow proof evidence and rejects it with typed failure reasons instead of relying on report text.
- Status-only report rows are rejected unless focused test, source scan, manifest, typed verifier, semantic assertion, no-mutation, fixture reference, and secret non-leak signals are all present.
- Non-empty diagnostics are rejected unless paired with positive no-issue proof, negative typed-category proof, and no-mutation/read-only audit proof.
- Secret-bearing fixtures require explicit non-leak assertions; diagnostic text containing `fixture-password` or `@example.invalid` is rejected.
- Fixture-only parsing is rejected unless every SB043 fixture is referenced by focused tests that also invoke typed verifier/request builder paths.
- The positive control loads the actual SB043 report, manifest, focused transcript, source scan, test source, and fixture files, and accepts only that source-backed proof set.

## Production Behavior Artifact Matrix
| Artifact | Producer | Consumer | Lifecycle | Negative-test citation |
| --- | --- | --- | --- | --- |
| Fake-proof evaluator | `ProcessDriverFakeProofResistanceTests` | SB044 focused tests and Gate O | Encodes rejection reasons for missing source-backed proof, missing semantic assertions, and leaked fixture secrets | `Process_driver_fake_proof_SB044_INV_001_rejects_status_only_and_non_empty_diagnostic_claims` |
| Secret leakage red-team case | `ProcessDriverFakeProofResistanceTests` | SB044 focused tests and future proof gates | Ensures fixture placeholders cannot appear in diagnostic/audit proof text without rejection | `Process_driver_fake_proof_SB044_INV_002_rejects_unredacted_secrets_and_fixture_only_parsing` |
| SB043 positive control | Actual SB043 proof files and corpus tests | SB044 focused tests and Gate O | Verifies the evaluator accepts only the source-backed SB043 closure with manifest, transcript, source scan, typed verifier coverage, and fixture references | `Process_driver_fake_proof_SB044_INV_003_accepts_only_source_backed_multi_domain_corpus_proof` |
| Source scan and anti-stub audit | SB044 PowerShell audit transcript | Bundle closure and downstream Gate O | Verifies required red-team tokens, fact count, issue coverage, actual SB043 proof dependency, no high-confidence secrets, no UI/media drift, and no stub markers | `bundle://proof/SB044/transcripts/fake-proof-resistance-source-scan-and-anti-stub-audit.txt` |

## Validation Results
- Focused fake-proof resistance tests passed: 3 passed, 0 failed, 0 skipped.
- Source scan and anti-stub audit passed.
- No UI/media drift occurred.
- No production source was changed for SB044.

## Reopen Triggers
- Reopen SB044 if future proof can pass from report status, non-empty diagnostics, fixture names, or package existence without source-backed test/scan/manifest artifacts.
- Reopen SB044 if secret-bearing fixtures or diagnostic/audit proof stop requiring explicit non-leak assertions.
- Reopen SB044 if new corpus domains or verifier lanes are added without extending fake-proof rejection coverage.
- Reopen SB044 if Gate O changes required semantic proof signals without updating the red-team evaluator.

## Closure Gate
- Entry gate: passed after SB043.
- Closure gate: passed.
- Progression decision: SB045 may proceed.
