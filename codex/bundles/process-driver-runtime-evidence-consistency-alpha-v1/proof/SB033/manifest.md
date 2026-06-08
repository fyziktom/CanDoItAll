# SB033 Proof Manifest

## Status
- Subbundle: $sb
- Status: Completed
- Owned requirement: $description
- Raw note: Review real code after crash and move toward stable Process Core with verification-only domain drivers.
- Semantic invariant contract: bundle://proof/SB033/semantic-invariants.md

## Changed File Manifest
- Changed-file hash manifest: bundle://proof/shared/changed-file-hashes.md
- Representative source proof: repo://tests/CanDoItAll.Tests.Unit/ProcessDriverContractPrerequisitesVerificationTests.cs

## Command Transcripts
- Closure proof index: bundle://proof/SB033/transcripts/closure-proof-index.txt
- Build transcript: bundle://proof/shared/transcripts/solution-build-no-restore.txt
- Passing transcript: bundle://proof/shared/transcripts/focused-process-driver-prerequisites-core-consumer-allowlist-test.txt
- Broad unit transcript: bundle://proof/shared/transcripts/unit-tests-excluding-stale-architecture-fixtures.txt
- Failing-first transcript: N/A process non-production compatibility closure; no new behavior changed inside this gate during the current execution pass.
- Anti-stub audit transcript: bundle://proof/shared/transcripts/source-boundary-and-anti-stub-audit.txt

## Source Assertions
- Production source assertion: repo://tests/CanDoItAll.Tests.Unit/ProcessDriverContractPrerequisitesVerificationTests.cs
- Boundary source assertion: repo://src/CanDoItAll.Processes.Core
- Process-module allowed adapter assertion: repo://tests/CanDoItAll.Tests.Integration/ProcessRuntimeEvidenceVerificationReadOnlyAdapterTests.cs

## Semantic Proof
- Invariant ID: $invariantId
- Shallow-pass trap: table-only, status-only, or non-empty diagnostic proof without source and negative checks.
- Adversarial negative proof: bundle://proof/SB033/transcripts/closure-proof-index.txt
- Semantic positive proof: bundle://proof/shared/transcripts/focused-process-driver-prerequisites-core-consumer-allowlist-test.txt
- Downstream smoke proof: bundle://proof/shared/transcripts/unit-tests-excluding-stale-architecture-fixtures.txt
- Red-team or verifier artifact: bundle://proof/shared/transcripts/source-boundary-and-anti-stub-audit.txt
