# SB045 Proof Manifest

## Status
- Subbundle: $sb
- Status: Completed
- Owned requirement: $description
- Raw note: Review real code after crash and move toward stable Process Core with verification-only domain drivers.
- Semantic invariant contract: bundle://proof/SB045/semantic-invariants.md

## Changed File Manifest
- Changed-file hash manifest: bundle://proof/shared/changed-file-hashes.md
- Representative source proof: repo://codex/bundles/process-driver-runtime-evidence-consistency-alpha-v1/architecture/04-driver-roadmap-and-release-gates.md

## Command Transcripts
- Closure proof index: bundle://proof/SB045/transcripts/closure-proof-index.txt
- Build transcript: bundle://proof/shared/transcripts/solution-build-no-restore.txt
- Passing transcript: bundle://proof/shared/transcripts/solution-build-no-restore.txt
- Broad unit transcript: bundle://proof/shared/transcripts/unit-tests-excluding-stale-architecture-fixtures.txt
- Failing-first transcript: N/A process non-production compatibility closure; no new behavior changed inside this gate during the current execution pass.
- Anti-stub audit transcript: bundle://proof/shared/transcripts/source-boundary-and-anti-stub-audit.txt

## Source Assertions
- Production source assertion: repo://codex/bundles/process-driver-runtime-evidence-consistency-alpha-v1/architecture/04-driver-roadmap-and-release-gates.md
- Boundary source assertion: repo://src/CanDoItAll.Processes.Core
- Process-module allowed adapter assertion: repo://tests/CanDoItAll.Tests.Integration/ProcessRuntimeEvidenceVerificationReadOnlyAdapterTests.cs

## Semantic Proof
- Invariant ID: $invariantId
- Shallow-pass trap: table-only, status-only, or non-empty diagnostic proof without source and negative checks.
- Adversarial negative proof: bundle://proof/SB045/transcripts/closure-proof-index.txt
- Semantic positive proof: bundle://proof/shared/transcripts/solution-build-no-restore.txt
- Downstream smoke proof: bundle://proof/shared/transcripts/unit-tests-excluding-stale-architecture-fixtures.txt
- Red-team or verifier artifact: bundle://proof/shared/transcripts/source-boundary-and-anti-stub-audit.txt
