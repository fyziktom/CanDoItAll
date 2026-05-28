# 07-tests-observability-and-final-hardening-review

## Status

- `Prepared`

## Objective

Complete the workflow hardening effort with deterministic tests, observability checks, documentation updates, and an architecture review that identifies any remaining gaps.

## Success Criteria

- Full build and targeted tests pass or blockers are recorded precisely.
- Workflow template pack tests cover all current templates.
- Compiler/adapter tests prove native MAF execution for representative graphs.
- Plugin executor tests cover fake success/failure/retry/cancellation/approval/artifact paths.
- Runtime event/artifact tests prove stable records.
- UI/browser proof exists if UI changed.
- Documentation is updated for workflow authoring, executor plugin contracts, runtime policy, approvals, and troubleshooting.
- Final review lists residual risks and follow-up bundles, if any.

## Covered Inputs

- R01 through R15

## Prerequisites

- SB01 through SB06 completed or explicitly scoped out with documented rationale.

## Exact Source References

- Entire workflow/agent/plugin surface found by SB01.
- `tests/CanDoItAll.Tests.Unit/`
- `tests/CanDoItAll.Tests.Integration/`
- `tests/CanDoItAll.Tests.Playwright/`
- `docs/`
- `.codex/bundles/workflow-maf-hardening/reviews/01-execution-report.md`

## Deliverables

- Final test transcript set.
- Observability/telemetry source assertions.
- Documentation updates.
- `reviews/02-final-architecture-review.md`
- Final execution report status.

## Implementation Steps

1. Run restore/build and targeted tests.
2. Run full relevant test suite if practical.
3. Run browser/Playwright proof if UI changed.
4. Inspect source for anti-patterns: duplicate runtime paths, raw object payloads, ungoverned plugin calls, missing approval checks, seed overwrite risk.
5. Update docs.
6. Create final architecture review with residual risks and follow-up recommendations.
7. Update `reviews/01-execution-report.md` to final state.

## Scope Exceptions

- Live external-service tests may remain manual if they require secrets. Mark them clearly.

## Do Not Do

- Do not mark the bundle complete if any critical runtime path bypasses validator/compiler/plugin policy gates.
- Do not bury failing tests in unrelated logs.
- Do not leave undocumented follow-up work for critical safety gaps.

## Acceptance Checklist

- Tests and proof are sufficient for reviewer confidence.
- Documentation matches implementation.
- Final review has no untriaged critical findings.

## Proof Required

- Build transcript.
- Unit/integration/browser test transcripts as applicable.
- Final architecture review.
- Final execution report.

## Progression Gate

This is the closure gate. If it cannot pass, create follow-up subbundles instead of claiming completion.

## Suggested Agent Prompt

```text
Implement SB07 only. Complete validation, observability, documentation, and final architecture review for the workflow MAF hardening bundle.
```
