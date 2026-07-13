# 07-performance-regression-and-architecture-closure

## Status

- `Ready`

## Objective

Close the generic MAF runtime architecture refactor with performance measurements, behavior parity proof, architecture boundary checks, and raw-note closure. Prove the new split is real, stable, testable, and does not regress startup/tool-composition behavior.

## Covered Inputs

- M002, M003, M005, M008, M010
- R010, R011, R012 plus closure of R001-R009

## Prerequisites

- SB01-SB06 progression gates passed.
- Proof manifests and semantic invariants exist for SB01-SB06.

## Exact Source References

- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime`
- `repo://tests/Unit/CanDoItAll.Tests.Unit`
- `repo://tests/Integration/CanDoItAll.Tests.Integration`
- `bundle://reviews/01-execution-report.md`
- `bundle://analysis/01-current-state.md`
- `bundle://traceability/01-requirement-traceability.md`

## Deliverables

- Before/after performance report for local runtime startup stages.
- Behavior parity test report for MAF runtime public flows and extracted collaborators.
- Architecture boundary scan preventing new driver/helper responsibilities from being added back into `MafAgentRuntime` where extracted collaborators exist.
- Reflection-reduction summary from SB06 integrated into closure.
- Raw-note closure table updated to `Solved`, `Partially solved`, or `Not solved`.
- Final validator result.

## Dependency Impact

- This is the closure subbundle. If it fails, reopen the earlier owning subbundle instead of closing with prose-only confidence.

## Validation Depth

- `Critical performance and architecture closure`

## Implementation Steps

1. Verify SB01-SB06 proof and progression gates.
2. Run targeted unit and integration tests for all touched runtime areas.
3. Capture before/after measurements for capability composition, provider enumeration, tool materialization, metadata resolution, filtering, session build, finalizer setup, and first external provider boundary.
4. Run architecture boundary checks over MAF runtime files and tests.
5. Verify no Financial Strategist/domain-specific implementation landed in this bundle.
6. Update raw-note closure and execution report.
7. Run prepared/completed validators as appropriate.

## Scope Exceptions

- External model/provider latency is not optimized here; it must be separated from local runtime composition measurements.
- Any unresolved architectural gap must become a blocker or follow-up subbundle, not residual prose.

## Do Not Do

- Do not claim performance improvement without measurement.
- Do not close if extracted collaborators are unused by production.
- Do not accept tests that only prove wrappers exist.
- Do not reintroduce agent-specific domain work.

## Acceptance Checklist

- [ ] Local runtime performance baseline and after-change comparison exists.
- [ ] Behavior parity tests pass.
- [ ] Architecture boundary checks pass.
- [ ] Reflection-heavy moved behavior is reduced or justified.
- [ ] Raw notes M001-M011 are closed honestly.
- [ ] Validator passes.

## Proof Required

- `proof/SB07/manifest.md`
- `proof/SB07/semantic-invariants.md`
- `## Production Behavior Artifact Matrix` for runtime measurements, diagnostics, contracts, and extracted production collaborators.
- Test transcripts.
- Performance measurement transcripts.
- Architecture boundary scan output.
- Final execution report update.
- Semantic Adequacy Gate: shallow-pass trap, adversarial negative proof, semantic positive proof, anti-stub audit, and raw-note literal closure.

## Browser Validation Logging

- N/A unless UI-visible diagnostics were added by earlier subbundles.

## Progression Gate

- The bundle can close only when performance, parity, testability, architecture boundaries, and raw-note closure are artifact-backed. If a closure claim is unsupported, reopen the owning subbundle.

## Suggested Agent Prompt

```text
Implement SB07 only after SB01-SB06 are complete. Prove MAF runtime architecture closure with performance measurements, parity tests, boundary scans, reflection-reduction summary, and raw-note closure. Do not add agent-specific domain work.
```
