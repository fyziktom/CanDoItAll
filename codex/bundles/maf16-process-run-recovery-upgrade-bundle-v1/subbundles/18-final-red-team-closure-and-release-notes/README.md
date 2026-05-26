# SB18: 18-final-red-team-closure-and-release-notes

## Status

- Status: `Completed`
- Owner: Codex execution

## Objective

- Final closure and release notes.

## Covered Inputs

- Normalized requirements mapped in `bundle://traceability/01-requirement-traceability.md` for SB18.
- Failed process run `9bbc0667-9d12-4506-ba81-654ef924cad6` where applicable to process-runtime phases.

## Prerequisites

- SB17 completed or explicitly reopened/blocked with dependency-safe notes.
- Root readiness gate must be valid for prepared-stage execution.

## Exact Source References

- bundle://reviews/01-execution-report.md
- repo://CanDoItAll.slnx

## Deliverables

- Run full build and focused test suites.
- Run red-team scenarios: stale artifact, wrong run, org-scoped current artifact, missing content hash, pending manager approval, QA mutation attempt, A2A/workflow mapping failure.
- Update process/agent skills and docs for MAF 1.6.
- Write a final release note: packages changed, APIs touched, process failure fixed, remaining risks.

## Dependency Impact

- Downstream phases may depend on this subbundle only after its closure gate records passing proof and any reopened risks.
- Critical behavior changes must update `bundle://proof/SB18/manifest.md` and `bundle://proof/SB18/semantic-invariants.md`.

## Validation Depth

- Run the tests, build, source assertions, changed-file hash capture, and anti-stub audit listed in the bundle proof contract.
- Browser validation: Required; capture API/UI browser proof and screenshot review.

## Implementation Steps

- Re-read the exact source references before editing.
- Make the smallest production or test change that satisfies the deliverables.
- Capture failing-first or adversarial evidence before accepting a behavior-changing fix.
- Capture passing proof and source assertions after the fix.

## Do Not Do

- Do not hard-code the Blazor/Tetris run as a special case.
- Do not weaken process genericity or bypass artifact validation to make a test pass.
- Do not silently skip MAF upgrade prerequisites or downstream validation.

## Acceptance Checklist

- Required work is implemented or explicitly blocked with a concrete follow-up path.
- Required proof artifacts exist under `bundle://proof/SB18/`.
- Entry and closure gate decisions are reflected in `bundle://reviews/01-execution-report.md`.

## Proof Required

- Failing-first or adversarial proof.
- Passing proof on production code path.
- Source assertions with exact repo paths.
- Anti-stub audit.
- Changed-file hashes.
- Notes on MAF 1.6 impact if this subbundle touches agent runtime.
- Notes on process core genericity if this subbundle touches Processes.

## Browser Validation Logging

- Add or update the SB18 row in `## Browser Validation Analytics` with route, viewport, evidence path, screenshots, and result, using N/A only when no UI/browser behavior changed.

## Progression Gate

- Do not close this subbundle until proof files under `proof/SB18` are updated and the next subbundle can safely depend on it.
- Next subbundle may start only when the closure gate is `Pass` or the execution report records an explicit dependency-safe block.

## Suggested Agent Prompt

- Execute SB18 from `bundle://subbundles/18-final-red-team-closure-and-release-notes/README.md`, keep scope limited to this phase, update proof and execution report before moving on.

