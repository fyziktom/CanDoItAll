# SB11 Proof Manifest

## Status

Completed.

## Goal

Consume all rejected artifact validation diagnostics in the runtime read model and keep rejected artifacts unsatisfied.

## Source References

- Source proof: repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessesService.RuntimeReadQuery.Support.cs
- Source proof: repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessHealthInvariantAuditor.cs
- Source proof: repo://src/CanDoItAll.Modules.Processes/Components/ProcessWorkspaceRunDetailsLoader.cs
- Source proof: repo://tests/CanDoItAll.Tests.Integration/ProcessRuntimeOperatorReadModelTests.cs

## Failing-first or adversarial proof

- Failing-first transcript: bundle://proof/SB11/transcripts/failing-first.txt

## Passing proof

- Passing transcript: bundle://proof/SB11/transcripts/passing.txt
- Passing transcript: bundle://proof/SB18/transcripts/process-runtime-operator-readmodel-tests.txt
- Passing transcript: bundle://proof/SB18/transcripts/build.txt
- Test name: `Runtime_read_model_exposes_all_rejected_recorded_artifact_finalizer_statuses`
- Test name: `Runtime_read_model_exposes_content_unavailable_artifact_obligations_for_recorded_but_unreadable_artifacts`

## Source assertions

- Source assertions transcript: bundle://proof/SB11/transcripts/source-assertions.txt

## Anti-stub audit

- Anti-stub audit transcript: bundle://proof/SB11/transcripts/anti-stub-audit.txt. No stubs or placeholder implementation markers were found in changed source/test files.

## Changed-file hashes

- repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessesService.RuntimeReadQuery.Support.cs SHA-256: 202e4d9860f49ebdc924d9c6a5b3e07e0694eea6c69684f996eeaf1e70008b4e
- repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessHealthInvariantAuditor.cs SHA-256: 694a313d3aa8dc0b549d86a545683f5c6d686c756154088de1c64c098d88a3e7
- repo://src/CanDoItAll.Modules.Processes/Components/ProcessWorkspaceRunDetailsLoader.cs SHA-256: 3035ea09eea1b77aaede54405436e482f6432ad2df420822afefa62cef852d96
- Full hash transcript: bundle://proof/SB11/transcripts/changed-file-hashes.txt

## Semantic invariants

- Contract: bundle://proof/SB11/semantic-invariants.md
