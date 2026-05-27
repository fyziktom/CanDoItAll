# SB10 Proof Manifest

## Status

Completed.

## Goal

Expand the artifact expectation read-model contract so every rejected finalizer status has a typed operator-visible value.

## Source References

- Source proof: repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessRuntimeViewModels.cs
- Source proof: repo://tests/CanDoItAll.Tests.Integration/ProcessRuntimeOperatorReadModelTests.cs

## Failing-first or adversarial proof

- Failing-first transcript: bundle://proof/SB10/transcripts/failing-first.txt

## Passing proof

- Passing transcript: bundle://proof/SB10/transcripts/passing.txt
- Passing transcript: bundle://proof/SB18/transcripts/process-runtime-operator-readmodel-tests.txt
- Passing transcript: bundle://proof/SB18/transcripts/build.txt
- Test name: `Runtime_read_model_exposes_all_rejected_recorded_artifact_finalizer_statuses`

## Source assertions

- Source assertions transcript: bundle://proof/SB10/transcripts/source-assertions.txt

## Anti-stub audit

- Anti-stub audit transcript: bundle://proof/SB10/transcripts/anti-stub-audit.txt. No stubs or placeholder implementation markers were found in changed source/test files.

## Changed-file hashes

- repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessRuntimeViewModels.cs SHA-256: cb571ed8481f96097023ef8bca8aa2d2c96e7e233a1c6e1eae52176fd10d1320
- repo://tests/CanDoItAll.Tests.Integration/ProcessRuntimeOperatorReadModelTests.cs SHA-256: 74cf48268df48b61338be929163f11e828b843e2ab9ffab13d7a8ea0487cf0f0
- Full hash transcript: bundle://proof/SB10/transcripts/changed-file-hashes.txt

## Semantic invariants

- Contract: bundle://proof/SB10/semantic-invariants.md
