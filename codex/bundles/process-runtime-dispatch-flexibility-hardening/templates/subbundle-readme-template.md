# Subbundle README Template

Use this template only when adding a follow-up subbundle to this bundle.

## Status

- `Ready`

## Objective

- State the concrete behavior-preserving outcome.

## Covered Inputs

- List requirement ids and raw note ids from `traceability/01-requirement-traceability.md`.

## Prerequisites

- List required prior subbundle gates and proof artifacts.

## Exact Source References

- `repo://path/to/source-file.cs`

## Deliverables

- List concrete code, test, DI, or documentation results.

## Dependency Impact

- State which downstream phases depend on this work and why weak proof would invalidate them.

## Validation Depth

- Use `Critical foundation`, `Process-critical closure`, or a narrower label with justification.

## Implementation Steps

1. Verify prerequisites and source references.
2. Make the smallest behavior-preserving changes.
3. Add focused tests and update DI.
4. Capture required proof.

## Scope Exceptions

- State any explicit exception. Use `none` only when true.

## Do Not Do

- Do not remove existing runtime behavior for cleanup-only reasons.
- Do not move domain-specific behavior into generic runtime/application layers.
- Do not add MAF, AgentFramework implementation, or `CanDoItAll.Modules.AgentFramework` references to any `src/Processes/*` project.
- Do not leave prompt composition, completion evidence policy, or actual step execution dispatch policy in generic Processes private helpers when the subbundle touches that behavior.

## Acceptance Checklist

- Focused tests prove the changed behavior.
- Existing relevant regression tests still pass.
- Dependency-direction scans pass when the subbundle touches driver, MAF, or process project boundaries.
- Diagnostics remain explicit and actionable.

## Proof Required

- `proof/SBxx/manifest.md`
- `proof/SBxx/semantic-invariants.md`
- Failing-first proof when behavior changes.
- Passing proof for the same invariant.
- Changed-file hashes, source assertions, command transcripts, and anti-stub audit output.

## Browser Validation Logging

- N/A unless UI/API/dashboard behavior changes. If it changes, record route, viewport, actions, assertions, screenshots, and result.

## Progression Gate

- State the exact evidence required before downstream work may continue.

## Suggested Agent Prompt

```text
Implement this subbundle only. Preserve behavior, keep generic process layers domain-neutral, keep MAF/AgentFramework below Processes in the dependency tree, add direct tests, capture artifact-backed proof, and stop if the progression gate cannot honestly pass.
```
