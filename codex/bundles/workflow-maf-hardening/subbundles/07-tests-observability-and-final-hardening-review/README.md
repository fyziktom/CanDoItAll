# 07-tests-observability-and-final-hardening-review

## Status

- `Completed`

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

- `repo://src/CanDoItAll.AgentFramework.Maf`
- `repo://src/CanDoItAll.Modules.AgentFramework`
- `repo://src/CanDoItAll.Modules.Plugins`
- `repo://src/CanDoItAll.Plugins.Abstractions`
- `repo://src/plugins`
- `repo://tests/CanDoItAll.Tests.Unit`
- `repo://tests/CanDoItAll.Tests.Integration`
- `repo://tests/CanDoItAll.Tests.Playwright`
- `repo://docs`
- `bundle://reviews/01-execution-report.md`

## Deliverables

- Final test transcript set.
- Observability/telemetry source assertions.
- Documentation updates.
- `reviews/02-final-architecture-review.md`
- Final execution report status.

## Dependency Impact

- Closes the bundle only after SB01 through SB06 proof is internally consistent and no critical runtime path bypasses the hardened gates.
- Converts any unresolved critical gap into a blocker or follow-up subbundle instead of a vague residual risk.
- Provides the final reviewer artifact that future work can use as the MAF workflow hardening baseline.

## Validation Depth

- Final closure phase with build, targeted tests, source assertions, anti-stub review, architecture review, browser proof when UI changed, and completed-stage bundle validation.
- Requires a final fake-proof resistance or verifier artifact across the completed critical subbundles.
- Live external-service proof remains optional/manual unless secrets and services are configured; deterministic fake proof is required.

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

## Browser Validation Logging

- Required if any UI files changed anywhere in the bundle; cite the SB06 browser evidence or add final Playwright proof.
- If UI files did not change, record a no-UI-change rationale in `reviews/01-execution-report.md`.

## Progression Gate

- This is the closure gate. If it cannot pass, create follow-up subbundles instead of claiming completion.

## Suggested Agent Prompt

```text
Implement SB07 only. Complete validation, observability, documentation, and final architecture review for the workflow MAF hardening bundle.
```
