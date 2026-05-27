# SB13 Proof Manifest

## Status

Completed.

## Goal

Make step detail health match finalizer validation.

## Semantic Invariant Contract

- `bundle://proof/SB13/semantic-invariants.md`

## Failing-first or adversarial proof

- `bundle://proof/SB13/transcripts/failing-first.txt`
- Invariant ID: `SB13-INV-001`
- Test name: `Runtime_read_model_exposes_content_unavailable_artifact_obligations_for_recorded_but_unreadable_artifacts`

## Passing proof

- `bundle://proof/SB13/transcripts/passing.txt`
- Invariant ID: `SB13-INV-001`
- Test name: `Runtime_read_model_exposes_content_unavailable_artifact_obligations_for_recorded_but_unreadable_artifacts`

## Source assertions

- `bundle://proof/SB13/transcripts/source-assertions.txt`
- `repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessesService.RuntimeReadQuery.cs`
- `repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessesService.RuntimeReadQuery.Support.cs`
- `repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessHealthInvariantAuditor.cs`
- `repo://tests/CanDoItAll.Tests.Integration/ProcessRuntimeOperatorReadModelTests.cs`

## Anti-stub audit

- `bundle://proof/SB13/transcripts/anti-stub-audit.txt`

## Changed-file hashes

- `bundle://proof/SB13/transcripts/changed-file-hashes.txt`
- `9C7A544DA0A55B580158793ECB735E7462B6C8D09AF1F905F68BFF0E23D25604` `repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessesService.RuntimeReadQuery.cs`
- `E32527D05B636E5357B7274AECBE47818A00F05DA70505A3E25AACA153CD8BAB` `repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessesService.RuntimeReadQuery.Support.cs`
- `3AF82541760DE698063AC9D86F6DE1CD8F2F44DE86023BED2589C09F4658584E` `repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessHealthInvariantAuditor.cs`
- `BF293F4090C5077C8850E3C703DA4284300B41ABEFAADDA8957AEA2A4A4DCAF6` `repo://tests/CanDoItAll.Tests.Integration/ProcessRuntimeOperatorReadModelTests.cs`
