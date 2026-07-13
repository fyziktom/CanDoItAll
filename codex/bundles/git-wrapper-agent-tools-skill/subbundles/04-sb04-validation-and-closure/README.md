# SB04-validation-and-closure

## Status

- `Completed`

## Objective

Run final validation, complete proof manifests, close the raw request note by note, and run bundle validators.

## Success Criteria

- Focused and affected broader tests pass or a concrete blocker is recorded.
- Critical proof manifests and semantic invariant files exist and cite real artifacts.
- Raw note closure table marks each item solved, partially solved, or not solved with evidence.
- Prepared and completed bundle validators pass.

## Covered Inputs

- REQ-008
- All raw request notes.

## Prerequisites

- SB01, SB02, and SB03 closure gates passed.
- `proof/SB01/manifest.md`, `proof/SB02/manifest.md`, and `proof/SB03/manifest.md` exist.

## Exact Source References

- `bundle://README.md`
- `bundle://reviews/01-execution-report.md`
- `bundle://traceability/01-requirement-traceability.md`
- `repo://tests/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj`
- `repo://tests/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj`

## Deliverables

- Final proof manifest under `proof/SB04/`.
- Final execution report update.
- Raw note closure audit.
- Final validator transcripts.

## Dependency Impact

- This is the final closure subbundle.
- If any proof is weak, reopen the owning earlier subbundle instead of closing with prose-only residual risk.

## Validation Depth

- Process-critical closure.
- Requires artifact-backed proof.

## Implementation Steps

1. Reopen raw input, requirements, traceability, phase plan, and prior manifests.
2. Run focused unit/integration tests for changed surfaces.
3. Run `git diff --check`.
4. Run anti-stub and excluded-operation grep checks.
5. Write final proof manifests and execution report rows.
6. Run prepared-stage and completed-stage bundle validators.
7. Mark root validation summary complete only when proof supports it.

## Scope Exceptions

- If a broad integration test is blocked by environment setup, record the exact command, failure, and replacement focused proof.

## Do Not Do

- Do not close a raw note with prose-only evidence.
- Do not hide failed validators as residual risk.
- Do not mutate production files after final tests without rerunning affected validation.

## Acceptance Checklist

- Every executed subbundle has gate rows and proof paths.
- Every critical subbundle has manifest and semantic invariant artifacts.
- Raw note closure statuses are evidence-backed.
- Bundle validators pass for prepared and completed stages.

## Proof Required

- `bundle://proof/SB04/manifest.md`
- `bundle://proof/SB04/transcripts/final-focused-tests.txt`
- `bundle://proof/SB04/transcripts/git-diff-check.txt`
- `bundle://proof/SB04/transcripts/bundle-validator-prepared.txt`
- `bundle://proof/SB04/transcripts/bundle-validator-completed.txt`

## Browser Validation Logging

- N/A - no browser-visible or host-visible UI behavior.

## Progression Gate

- Close the workflow only when final validators pass.
- All raw notes must be closed with artifact-backed evidence.

## Suggested Agent Prompt

```text
Implement SB04 only. Reopen all prior proof, run final validation, repair weak or missing evidence by reopening the owning subbundle, update the execution report and root status, run prepared and completed bundle validators, and close only with artifact-backed raw note closure.
```
