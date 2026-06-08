# SB009 Proof Manifest

## Status
- Subbundle: `SB009`
- Status: `Completed`
- Critical gate: `Gate C`
- Owned requirement: `REQ-003`
- Scope result: Core/driver API governance and reverse-dependency scans pass with reflected public API snapshots, clean build, focused tests, and runtime-host denial proof.

## Changed File Hashes
| File | SHA-256 |
| --- | --- |
| `repo://codex/bundles/process-driver-multi-domain-verification-gateway-v1/architecture/06-core-public-api-owner-classification.md` | `42b5c11cd28486d259113f0c0f406a63386332cd12f42cf547972457ee9fb113` |
| `repo://codex/bundles/process-driver-multi-domain-verification-gateway-v1/architecture/07-driver-abstraction-api-versioning-snapshot.md` | `55f039557e96bc3a340f8edf8cb1947328c514d075cf15956b861c07e1904bce` |
| `repo://tests/CanDoItAll.Tests.Unit/ProcessDriverContractApiVerificationBoundaryTests.cs` | `08cea57790bbb65214e0b1ad7c626b8062ebc7793a988b5e0818c63cbcc29e54` |
| `repo://codex/bundles/process-driver-multi-domain-verification-gateway-v1/subbundles/sb009-gate-c-core-driver-api-governance-and-reverse-dependency-scans-pass/README.md` | `d4c67e2adc72b6af7a40fd8965eedfeaa57d70c24f6b1d8b0434db62e0ade746` |
| `repo://codex/bundles/process-driver-multi-domain-verification-gateway-v1/proof/SB009/semantic-invariants.md` | `74c0593442ae2c9448fdb17c29dff02244ce144c10e54b5f2faf09f493f6f781` |
| `repo://codex/bundles/process-driver-multi-domain-verification-gateway-v1/reviews/01-execution-report.md` | `42d1675c16809cd0d98cb46b48eceeb6779a2ff776dd0e5b3677bfc0bf2de267` |

## Command Transcripts
- Solution build: `bundle://proof/SB009/transcripts/gate-c-solution-build-no-restore.txt`
- Focused API boundary tests: `bundle://proof/SB009/transcripts/gate-c-focused-contract-api-boundary-tests.txt`
- API governance/reverse-dependency scan: `bundle://proof/SB009/transcripts/gate-c-api-governance-reverse-dependency-scan.txt`
- Red-team report-only rejection: `bundle://proof/SB009/transcripts/red-team-report-only-api-governance-rejection.txt`
- Semantic positive proof index: `bundle://proof/SB009/transcripts/gate-c-proof-index.txt`

## Source Assertions
- Core snapshot documents 64 public types and ordinal surface hash `99e2a6a6033d749f388a440360e4ef6db5b92c1d1fb2949a9f22d321ccd606d1`.
- Driver abstraction snapshot documents contract version `1.2.0`, 28 public types, and ordinal surface hash `2c4e557a2e0118a4a64f60f18830dd25c365ecca2939a3f4567084fb132be5fc`.
- Core references `CanDoItAll.Processes.Contracts` and does not reference driver packages.
- Driver abstractions have no project or package references.
- TranscriptVerification depends only on driver abstractions.
- RuntimeEvidence keeps the explicit Core descriptor plus driver-abstraction dependency.
- Core and driver abstractions expose no runtime host, registry, selector, DI, provider, or manager-command surface.

## Validation Results
- Solution build passed: 0 warnings, 0 errors, exit code 0.
- Focused API boundary tests passed: 10 passed, 0 failed, 0 skipped.
- Reverse-dependency and runtime-host scan passed.
- Red-team negative proof rejected report-only API governance closure.
- Semantic positive proof verified all required artifacts and upstream manifests.

## Production Behavior Artifact Matrix
| Artifact | Producer | Consumer | Lifecycle | Negative-test citation |
| --- | --- | --- | --- | --- |
| Core public API owner snapshot | `architecture/06-core-public-api-owner-classification.md` | Gate C and compatibility gates | Records live Core public type count/hash and ownership rules | `bundle://proof/SB009/transcripts/red-team-report-only-api-governance-rejection.txt` |
| Driver abstraction versioning snapshot | `architecture/07-driver-abstraction-api-versioning-snapshot.md` | Gate C and future driver packages | Records driver abstraction contract version, public type count/hash, and runtime-surface denial | `bundle://proof/SB009/transcripts/gate-c-api-governance-reverse-dependency-scan.txt` |
| Reverse-dependency scan | Gate C source scan transcript | Gate C proof index | Proves Core has no driver dependency and abstractions remain dependency-free | `bundle://proof/SB009/transcripts/gate-c-proof-index.txt` |

## Reopen Triggers
- Reopen SB007/SB009 when Core public type count/hash changes or Core gains a driver/runtime-host dependency.
- Reopen SB008/SB009 when driver abstraction public type count/hash/version changes or abstractions gain dependencies/runtime surfaces.
- Reopen SB009 and downstream phases if the reverse-dependency scan or focused API boundary tests fail.

## Closure Gate
- Entry gate: passed after SB008.
- Closure gate: passed.
- Progression decision: SB010 may proceed.
