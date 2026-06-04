# SB09 - Final red-team and next-phase readiness

## Status

- Status: Completed

## Objective

Run final fake-proof and hidden-dependency audit, then prepare the handoff for the next bundle: process contracts/core split and later driver-pack foundation.

## Covered Inputs

- User request to decouple MAF from Processes in small safe steps.
- `inputs/01-source-artifacts.md`
- `analysis/01-current-state.md`
- `inventories/01-process-tool-parity-inventory.md`
- `evidence/checklists/MAF_Processes_Decoupling_Checklists.xlsx`

## Prerequisites

- SB08 closure gate passed.
- All critical proof manifests exist.

## Exact Source References

- `repo://src/CanDoItAll.AgentFramework.Maf`
- `repo://src`
- `repo://src/CanDoItAll.Modules.Processes`
- `repo://tests`
- `bundle://proof`
- `bundle://proof`

## Deliverables

- Final red-team audit artifact.
- Hidden dependency scan transcript.
- Final all-target build/test summary.
- Next-phase readiness note for process contracts/core split.
- Bundle execution report completed.

## Dependency Impact

- This closes the bundle and determines whether the next refactor can begin safely.


## Validation Depth

- Critical foundation. Requires semantic adequacy proof, artifact-backed manifest, source assertions, anti-stub audit, and downstream smoke where named in the progression gate.


## Implementation Steps

1. Run hidden dependency scans for `CanDoItAll.Modules.Processes` under MAF.
2. Run scans for old process builder names under MAF.
3. Run process tool parity tests and policy tests again.
4. Run full build.
5. Run targeted integration/process smoke tests.
6. Audit proof manifests for missing hashes/transcripts.
7. Write `reviews/02-final-red-team-review.md`.
8. Write `architecture/04-next-phase-readiness.md`.

## Scope Exceptions

- Full process-core split is intentionally out of scope.
- Full driver-pack architecture is intentionally out of scope.

## Do Not Do

- Do not change process dispatcher behavior.
- Do not start process core extraction.
- Do not introduce DotNet/SWDev/business process drivers.
- Do not remove or rename any process tool.

## Acceptance Checklist

- [x] No hidden MAF -> Processes dependency.
- [x] Old process builder names are gone from MAF.
- [x] All process tools preserved.
- [x] All critical proof manifests complete.
- [x] Final red-team artifact written.
- [x] Next-phase readiness note written.

## Closure Notes

- Entry gate: Passed. SB08 documentation handoff closed before final red-team.
- Validation: Hidden dependency scan, MAF static dependency guard, provider/policy unit tests, provider composition integration tests, capability filtering tests, process outbox, receipt semantics, artifact-lineage smoke, proof audit, and final solution build passed.
- Browser validation: N/A. SB09 is closure/proof work with no rendered UI route exercised.
- Proof: `bundle://proof/SB09/manifest.md`, `bundle://proof/SB09/semantic-invariants.md`, `bundle://reviews/02-final-red-team-review.md`, and `bundle://architecture/04-next-phase-readiness.md`.
- Progression gate: Passed. The bundle may close; the next bundle should start with the SB09 smoke set.

## Proof Required

- `dotnet build CanDoItAll.slnx` final transcript
- Targeted unit/integration transcript bundle
- Hidden dependency scan transcript
- `reviews/02-final-red-team-review.md`
- `architecture/04-next-phase-readiness.md`
- `proof/SB09/manifest.md`
- `proof/SB09/semantic-invariants.md`

## Browser Validation Logging

- N/A. Final closure did not exercise or change a rendered UI route.


## Progression Gate

- Passed for the scoped objective: direct MAF process-tool coupling is removed, guarded, documented, and runtime-smoked.


## Suggested Agent Prompt

Use `shared-prompts/implementation-prompt.md`. Focus only on SB09. Do not start the next subbundle until the SB09 closure gate passes and proof artifacts are written.
