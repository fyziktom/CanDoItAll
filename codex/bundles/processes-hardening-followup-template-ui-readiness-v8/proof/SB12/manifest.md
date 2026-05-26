# SB12 Proof Manifest

## Status

Completed.

## Production-path coverage

- `repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessBlockStateClassifier.cs` now prefers explicit typed `BlockCause` and only infers a cause from prose when no typed cause is supplied.
- `repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessStepRunBlockState.cs` carries the effective typed or legacy-inferred cause into recovery routing and evidence fingerprints.
- `repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessesService.Runtime.StepTransitions.cs` persists block reason code, recovery options, next recovery action, and recovery routing journal entries from the shared block-state path.
- `repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessesService.RuntimeReadQuery.cs` and `.Support.cs` expose block reason code, recovery options, next recovery action, and step health through run details.
- `repo://tests/CanDoItAll.Tests.Integration/ApiIntegrationTests.cs` verifies HTTP run detail exposes upstream missing-artifact recovery health.

## Semantic invariant

See `bundle://proof/SB12/semantic-invariants.md`.

## Failing-first or adversarial proof

`bundle://proof/SB12/transcripts/failing-first.txt`

## Passing proof

`bundle://proof/SB12/transcripts/passing.txt`

## Source assertions

`bundle://proof/SB12/transcripts/source-assertions.txt`

## Anti-stub audit

`bundle://proof/SB12/transcripts/anti-stub-audit.txt`

## Changed-file hashes

`bundle://proof/SB12/transcripts/changed-file-hashes.txt`
