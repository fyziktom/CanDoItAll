# Corrective subbundle template

## Status

- `Completed`
- `2026-04-13`: not instantiated because no gate or proof step failed during live execution.

## Objective

- Instantiate a validator-compliant corrective subbundle whenever a gate or proof step fails so downstream work stays blocked until the defect is actually repaired.

## Covered Inputs

- `U007` Add repeated architecture reviews and corrective paths.
- `BRQ-016` Repeated architecture review gates.
- `BRQ-017` Corrective-first continuation.

## Prerequisites

- A review gate, proof command, browser check, or host validation has failed.
- The failing evidence is captured before any downstream subbundle resumes.

## Exact Source References

- C:\repositories\CanDoItAll\architecture_hardening_bundle\templates\corrective-subbundle-template.md
- C:\repositories\CanDoItAll\architecture_hardening_bundle\templates\review-gate-memo-template.md
- C:\repositories\CanDoItAll\architecture_hardening_bundle\reviews\01-execution-report.md
- C:\repositories\CanDoItAll\architecture_hardening_bundle\reviews\02-architecture-gate-memo-log.md

## Deliverables

- A concrete corrective subbundle README populated from the template.
- Captured failing evidence and a narrowed root-cause statement.
- An explicit rerun list for the failed validation and any dependent proof.
- Updated execution-report and gate-memo entries that keep downstream work blocked until closure.

## Dependency Impact

- Every review gate depends on this template being usable when proof fails.
- Weak corrective capture would let downstream work borrow trust from unclosed defects.

## Validation Depth

- `Corrective governance gate`

## Implementation Steps

1. Capture the failing gate or proof and the evidence that made it fail.
2. Instantiate the corrective subbundle from the corrective template.
3. Populate exact source references, scope, rerun commands, and unblock condition.
4. Apply the smallest correction that truly closes the defect.
5. Rerun the failed validation and any dependent validations.
6. Update the execution report and architecture gate memo before unblocking downstream work.

## Do Not Do

- Do not continue downstream implementation on a failed or weak gate.
- Do not summarize a corrective need in prose without creating a concrete corrective subbundle.
- Do not mark the corrective subbundle complete until the failed gate is rerun.

## Acceptance Checklist

- A corrective subbundle exists and names the real failing gate or proof step.
- The root cause and correction scope are explicit.
- The rerun validation list is concrete and complete enough to re-establish trust.
- Execution-report and gate-memo records explain why downstream work remains blocked.

## Proof Required

- The exact failing command, browser proof, or host check that triggered the corrective path.
- The corrective subbundle README created from the template.
- Updated `reviews/01-execution-report.md` and `reviews/02-architecture-gate-memo-log.md`.
- Successful rerun of the failed gate or proof step before downstream work resumes.

## Browser Validation Logging

- If the failure is UI-related, capture the affected route, viewport, Playwright actions, screenshots, and visual review answers in the corrective subbundle.
- If the failure is non-UI, record `N/A` explicitly in the corrective subbundle so the absence of browser proof is intentional.

## Progression Gate

- Downstream work may resume only after the corrective subbundle is completed, the failed validation is rerun, and the blocking gate is recorded as `Passed`.

## Suggested Agent Prompt

```text
Create and execute a corrective subbundle for the failed gate only. Capture the failing evidence first, apply the smallest real fix, rerun the blocked validations, and do not unblock downstream work until the gate passes.
```
