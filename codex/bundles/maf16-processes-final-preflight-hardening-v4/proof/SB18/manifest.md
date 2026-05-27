# SB18 Proof Manifest

## Status

Completed.

## Goal

Produce an evidence-backed go/no-go report and block full real UI process testing when validation remains incomplete.

## Source References

- Source proof: repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessesService.RuntimeReadQuery.Support.cs
- Source proof: repo://tests/CanDoItAll.Tests.Integration/ProcessRuntimeOperatorReadModelTests.cs
- Source proof: bundle://reviews/01-execution-report.md

## Failing-first or adversarial proof

- Failing-first transcript: bundle://proof/SB18/transcripts/integration-filter-tests.txt

## Passing proof

- Passing transcript: bundle://proof/SB18/transcripts/passing.txt
- Passing transcript: bundle://proof/SB18/transcripts/restore.txt
- Passing transcript: bundle://proof/SB18/transcripts/build.txt
- Passing transcript: bundle://proof/SB18/transcripts/unit-filter-tests.txt
- Passing transcript: bundle://proof/SB18/transcripts/process-runtime-operator-readmodel-tests.txt
- Passing transcript: bundle://proof/SB18/transcripts/component-process-tests.txt
- Test name: `Runtime_read_model_exposes_all_rejected_recorded_artifact_finalizer_statuses`

## Source assertions

- Source assertions transcript: bundle://proof/SB18/transcripts/source-assertions.txt

## Anti-stub audit

- Anti-stub audit transcript: bundle://proof/SB18/transcripts/anti-stub-audit.txt. No stubs or placeholder implementation markers were found in changed source/test files.

## Changed-file hashes

- repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessesService.RuntimeReadQuery.Support.cs SHA-256: 202e4d9860f49ebdc924d9c6a5b3e07e0694eea6c69684f996eeaf1e70008b4e
- repo://tests/CanDoItAll.Tests.Integration/ProcessRuntimeOperatorReadModelTests.cs SHA-256: 74cf48268df48b61338be929163f11e828b843e2ab9ffab13d7a8ea0487cf0f0
- Full hash transcript: bundle://proof/SB18/transcripts/changed-file-hashes.txt

## Semantic invariants

- Contract: bundle://proof/SB18/semantic-invariants.md
