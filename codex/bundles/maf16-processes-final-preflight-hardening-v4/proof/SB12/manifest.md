# SB12 Proof Manifest

## Status

Completed.

## Goal

Expose rejected artifact diagnostics, finalizer metadata, and danger tones in process operator surfaces.

## Source References

- Source proof: repo://src/CanDoItAll.Modules.Processes/Components/ProcessWorkspaceRunsArtifactsSection.razor
- Source proof: repo://src/CanDoItAll.Modules.Processes/Components/LiveProcessesDashboard.razor
- Source proof: repo://src/CanDoItAll.Modules.Processes/Components/ProcessCanvasSelectionPanel.razor
- Source proof: repo://src/CanDoItAll.Modules.Processes/Components/ProcessWorkspaceRunDetailsLoader.cs

## Failing-first or adversarial proof

- Failing-first transcript: bundle://proof/SB12/transcripts/failing-first.txt

## Passing proof

- Passing transcript: bundle://proof/SB12/transcripts/passing.txt
- Passing transcript: bundle://proof/SB18/transcripts/component-process-tests.txt
- Passing transcript: bundle://proof/SB18/transcripts/process-runtime-operator-readmodel-tests.txt
- Passing transcript: bundle://proof/SB12/browser-live-processes-route.png
- Test name: `Runtime_read_model_exposes_all_rejected_recorded_artifact_finalizer_statuses`

## Source assertions

- Source assertions transcript: bundle://proof/SB12/transcripts/source-assertions.txt

## Anti-stub audit

- Anti-stub audit transcript: bundle://proof/SB12/transcripts/anti-stub-audit.txt. No stubs or placeholder implementation markers were found in changed source/test files.

## Changed-file hashes

- repo://src/CanDoItAll.Modules.Processes/Components/ProcessWorkspaceRunsArtifactsSection.razor SHA-256: d3d4e1075e34bf58a7076d5149aabac37037f4962f592ab6a58b2e10485d3db8
- repo://src/CanDoItAll.Modules.Processes/Components/LiveProcessesDashboard.razor SHA-256: eba7d849d0bd865f510876d892a6feca51c09bc7169a153ff856f7d5ab70f88c
- repo://src/CanDoItAll.Modules.Processes/Components/ProcessCanvasSelectionPanel.razor SHA-256: fb25431b44afd0ef15ed44b3c137d90f9728068aba145abc99a2fa41a75913c8
- Full hash transcript: bundle://proof/SB12/transcripts/changed-file-hashes.txt

## Semantic invariants

- Contract: bundle://proof/SB12/semantic-invariants.md
