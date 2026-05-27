# SB10: 10-process-artifact-dedupe-scope-correctness

## Goal

Fix/prove artifact dedupe scope correctness.

## Required work

- Inspect `RecordArtifactAsync` projection identity and external reference dedupe queries.
- Ensure dedupe is scoped to process run + compatible step run + compatible artifact expectation, or returns a collision error.
- Add tests: same run/different step same identity, same run/different expectation same external reference, same step/same expectation same identity.
- Do not rely on projection identity hash alone unless it includes step and expectation and tests prove it.

## Required proof

- Failing-first or adversarial proof.
- Passing production-path proof.
- Source assertions with exact repo paths.
- Anti-stub audit.
- Changed-file hashes.
- Classification: MAF package-level / MAF adapter-level / process runtime-level / template/UI-level.
- Note whether this subbundle changes behavior or only improves proof/documentation.

## Closure criteria

This subbundle is complete only when proof files under `proof/SB10` are updated and downstream subbundles can rely on it.

## Status

- Completed

## Objective

Prove artifact dedupe does not bind projection identity across the wrong step expectation.

## Covered Inputs

- RQ07 artifact dedupe scope.

## Prerequisites

- Source audit confirms the risk boundary.

## Exact Source References

- `repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessesService.Runtime.Operations.cs`
- `repo://tests/CanDoItAll.Tests.Integration/ProcessesServiceIntegrationTests.cs`

## Deliverables

- Existing scope regression rerun and proof manifest updated.

## Dependency Impact

- SB11 and SB13 rely on correct artifact identity binding.

## Validation Depth

- Integration test with wrong-step expectation collision.

## Implementation Steps

- Re-run the scope collision regression.
- Record source and test proof in `proof/SB10`.

## Do Not Do

- Do not accept process-run-wide dedupe as sufficient.

## Acceptance Checklist

- Wrong-scope projection identity is rejected.

## Proof Required

- `proof/SB10/manifest.md` and `proof/SB10/semantic-invariants.md`.

## Browser Validation Logging

- No browser route is affected.

## Progression Gate

- Dedupe scope proof must pass before content policy proof.

## Suggested Agent Prompt

Verify projection identity and external reference reuse cannot cross step expectation scope.
