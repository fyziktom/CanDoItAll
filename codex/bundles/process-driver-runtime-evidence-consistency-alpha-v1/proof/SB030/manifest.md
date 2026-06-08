# SB030 Proof Manifest

## Status
- Subbundle: $sb
- Status: Completed
- Owned requirement: $description
- Raw note: Review real code after crash and move toward stable Process Core with verification-only domain drivers.
- Semantic invariant contract: bundle://proof/SB030/semantic-invariants.md

## Changed File Manifest
- Changed-file hash manifest: bundle://proof/shared/changed-file-hashes.md
- Representative source proof: repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRuntimeEvidenceVerificationReadOnlyAdapter.cs

## Command Transcripts
- Closure proof index: bundle://proof/SB030/transcripts/closure-proof-index.txt
- Build transcript: bundle://proof/shared/transcripts/solution-build-no-restore.txt
- Passing transcript: bundle://proof/SB030/transcripts/passing-runtime-evidence-adapter-after-architecture-guard.txt
- Broad unit transcript: bundle://proof/shared/transcripts/unit-tests-excluding-stale-architecture-fixtures.txt
- Failing-first transcript: bundle://proof/SB030/transcripts/failing-first-runtime-evidence-adapter-before-implementation.txt
- Anti-stub audit transcript: bundle://proof/shared/transcripts/source-boundary-and-anti-stub-audit.txt

## Source Assertions
- Production source assertion: repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRuntimeEvidenceVerificationReadOnlyAdapter.cs
- Boundary source assertion: repo://src/CanDoItAll.Processes.Core
- Process-module allowed adapter assertion: repo://tests/CanDoItAll.Tests.Integration/ProcessRuntimeEvidenceVerificationReadOnlyAdapterTests.cs

## Semantic Proof
- Invariant ID: $invariantId
- Shallow-pass trap: table-only, status-only, or non-empty diagnostic proof without source and negative checks.
- Adversarial negative proof: bundle://proof/SB030/transcripts/closure-proof-index.txt
- Semantic positive proof: bundle://proof/SB030/transcripts/passing-runtime-evidence-adapter-after-architecture-guard.txt
- Downstream smoke proof: bundle://proof/shared/transcripts/unit-tests-excluding-stale-architecture-fixtures.txt
- Red-team or verifier artifact: bundle://proof/shared/transcripts/source-boundary-and-anti-stub-audit.txt
