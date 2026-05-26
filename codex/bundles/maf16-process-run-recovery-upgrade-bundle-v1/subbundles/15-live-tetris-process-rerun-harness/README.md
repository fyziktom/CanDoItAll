# SB15: 15-live-tetris-process-rerun-harness

## Status

- Status: `Completed`
- Owner: Codex execution

## Objective

- Rerun the live Tetris/Blazor process after fixes.

## Covered Inputs

- Normalized requirements mapped in `bundle://traceability/01-requirement-traceability.md` for SB15.
- Failed process run `9bbc0667-9d12-4506-ba81-654ef924cad6` where applicable to process-runtime phases.

## Prerequisites

- SB14 completed or explicitly reopened/blocked with dependency-safe notes.
- Root readiness gate must be valid for prepared-stage execution.

## Exact Source References

- repo://tests/CanDoItAll.Tests.Playwright/AgentFrameworkAuditProofTests.cs
- bundle://inputs/03-api-evidence-index.md

## Deliverables

- Use the live-run profile, not seeded baseline transitions/artifacts.
- Start the process and verify step 0 completes with a valid current-run delivery contract artifact.
- Verify implementation step is the first step allowed to mutate product files.
- Verify validation step cannot mutate product files and captures runtime/browser evidence.
- Capture run detail, step details, artifacts, execution logs, tool receipts, and diagnostics as proof.

## Dependency Impact

- Downstream phases may depend on this subbundle only after its closure gate records passing proof and any reopened risks.
- Critical behavior changes must update `bundle://proof/SB15/manifest.md` and `bundle://proof/SB15/semantic-invariants.md`.

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
- Required proof artifacts exist under `bundle://proof/SB15/`.
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

- Add or update the SB15 row in `## Browser Validation Analytics` with route, viewport, evidence path, screenshots, and result, using N/A only when no UI/browser behavior changed.

## Progression Gate

- Do not close this subbundle until proof files under `proof/SB15` are updated and the next subbundle can safely depend on it.
- Next subbundle may start only when the closure gate is `Pass` or the execution report records an explicit dependency-safe block.

## Suggested Agent Prompt

- Execute SB15 from `bundle://subbundles/15-live-tetris-process-rerun-harness/README.md`, keep scope limited to this phase, update proof and execution report before moving on.

