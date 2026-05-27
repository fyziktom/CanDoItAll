# SB10 Proof Manifest

## Status

Completed.

## Goal

Fix/prove artifact dedupe scope correctness.

## Semantic Invariant Contract

- `bundle://proof/SB10/semantic-invariants.md`

## Failing-first or adversarial proof

- `bundle://proof/SB10/transcripts/failing-first.txt`
- Invariant ID: `SB10-INV-001`
- Test name: `RecordArtifactAsync_SB11_INV_001_rejects_projection_identity_for_wrong_step_expectation_scope`

## Passing proof

- `bundle://proof/SB10/transcripts/passing.txt`
- Invariant ID: `SB10-INV-001`
- Test name: `RecordArtifactAsync_SB11_INV_001_rejects_projection_identity_for_wrong_step_expectation_scope`

## Source assertions

- `bundle://proof/SB10/transcripts/source-assertions.txt`
- `repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessesService.Runtime.Operations.cs`
- `repo://tests/CanDoItAll.Tests.Integration/ProcessesServiceIntegrationTests.cs`

## Anti-stub audit

- `bundle://proof/SB10/transcripts/anti-stub-audit.txt`

## Changed-file hashes

- `bundle://proof/SB10/transcripts/changed-file-hashes.txt`
- `EE2154A3C026E749BED344F798887FB5B1633CD644751BF4DFE25901E1D931FD` `repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessesService.Runtime.Operations.cs`
- `61ADE5D9098CB0549F2AAD53A8CC381B88D0785A0263CDE8EBDCBE418BA2CC29` `repo://tests/CanDoItAll.Tests.Integration/ProcessesServiceIntegrationTests.cs`
