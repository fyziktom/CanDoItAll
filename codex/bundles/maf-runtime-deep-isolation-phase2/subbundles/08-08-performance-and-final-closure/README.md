# SB08 Performance And Final Closure

## Status

- `Ready`

## Objective

Complete closure proof: verify `MafAgentRuntime` is now thin, behavior parity holds, startup/capability composition performance is measured, and remaining residuals are explicit instead of hidden.

## Covered Inputs

- N001, N003, N005, N006, N007
- MAF2-R009, MAF2-R013, MAF2-R014

## Prerequisites

- SB01-SB07 closure proof.

## Exact Source References

- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/MafAgentRuntime.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/MafRuntimeServiceCollectionExtensions.cs`
- `repo://tests/Unit/CanDoItAll.Tests.Unit`
- `repo://tests/Integration/CanDoItAll.Tests.Integration`
- `bundle://reviews/01-execution-report.md`

## Deliverables

- Final boundary scan proving no forbidden hidden runtime builders remain.
- Runtime size/member-count comparison against SB01 baseline.
- Capability composition performance/startup measurement before/after or implementation-time baseline if no pre-change measurement exists.
- Focused unit and integration proof.
- Full-suite run or explicit baseline failure record.
- Updated execution report with raw-note closure.

## Dependency Impact

- This is the final gate.
- It decides whether the bundle can close or must reopen an earlier subbundle.

## Validation Depth

- Critical final closure.
- Requires Semantic Adequacy Gate proof.

## Implementation Steps

1. Run final source scans and architecture guard tests.
2. Run MAF project build.
3. Run focused MAF runtime unit suite.
4. Run MAF handoff/runtime integration slice.
5. Run or document full unit/integration baseline status.
6. Capture composition/startup timing evidence using existing metrics or focused harness.
7. Update execution report and proof manifests.

## Scope Exceptions

- Do not fix unrelated full-suite failures unless implementation introduced them.

## Do Not Do

- Do not claim full closure if private runtime builders remain.
- Do not claim performance improvement without measurement.
- Do not hide broad baseline failures.

## Acceptance Checklist

- `MafAgentRuntime` is a thin adapter by source scan and review.
- No hidden private builders remain under runtime.
- Direct collaborator tests pass.
- MAF handoff/runtime integration slice passes.
- Performance evidence is recorded.
- Raw notes N001-N008 are closed or explicitly marked with residuals.

## Proof Required

- `proof/SB08/manifest.md`
- `proof/SB08/semantic-invariants.md`
- Build transcript.
- Unit/integration test transcripts.
- Performance metric transcript.
- Final boundary scan.
- Execution report update.

## Browser Validation Logging

- N/A unless UI-visible runtime diagnostics were added during implementation.

## Progression Gate

- Bundle can close only after all acceptance checklist items are true and residuals are explicitly owned.

## Suggested Agent Prompt

```text
Implement SB08 only. Do not make broad refactors. Prove final architecture closure, performance behavior, and test status. Reopen earlier subbundles if boundary scans fail.
```
