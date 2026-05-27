# SB11 Proof Manifest

## Scope

- Subbundle: SB11 - Artifact content hash and storage reference proof.
- Invariant ID: SB11-INV-001
- Shipped behavior: Projection identity and external reference deduplication is scoped to the requested step and expectation.

## Source Proof

- repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessesService.Runtime.Operations.cs
- repo://tests/CanDoItAll.Tests.Integration/ProcessesServiceIntegrationTests.cs
- bundle://proof/SB11/semantic-invariants.md
- bundle://analysis/04-maf16-feature-adoption-matrix.md

## Command Transcripts

- Passing transcript: bundle://proof/SB11/transcripts/passing.txt
- Adversarial negative proof transcript: bundle://proof/SB11/transcripts/failing-first.txt
- Anti-stub audit transcript: bundle://proof/SB11/transcripts/anti-stub-audit.txt
- Source assertions transcript: bundle://proof/SB11/transcripts/source-assertions.txt
- Changed-file hashes transcript: bundle://proof/SB11/transcripts/changed-file-hashes.txt
- Test name: CanDoItAll.Tests.Integration.ProcessesServiceIntegrationTests.RecordArtifactAsync_SB11_INV_001_rejects_projection_identity_for_wrong_step_expectation_scope

## Changed File Hashes

- repo://codex/bundles/maf16-processes-real-usage-hardening-v2/analysis/04-maf16-feature-adoption-matrix.md: 19B3DD358326D819E0D890A76F8111A622DF45D513D05EF0118F221ED946DBDB
- repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessesService.Runtime.Operations.cs: EE2154A3C026E749BED344F798887FB5B1633CD644751BF4DFE25901E1D931FD
- repo://tests/CanDoItAll.Tests.Integration/ProcessesServiceIntegrationTests.cs: 61ADE5D9098CB0549F2AAD53A8CC381B88D0785A0263CDE8EBDCBE418BA2CC29
