# SB09: 09-refactor-checkpoint-a-maf-adapter-clean-boundary

## Goal

Refactor MAF adapter after feature-adoption audit.

## Required work

- Move MAF 1.6 compatibility/adoption helpers into a small boundary layer.
- Keep Processes and domain runtime models independent from MAF internals.
- Update docs: package version policy, adopted features, deferred features, and upgrade watch for MAF 1.7.

## Required proof

- Failing-first or adversarial proof.
- Passing production-path proof.
- Source assertions with exact repo paths.
- Anti-stub audit.
- Changed-file hashes.
- Classification: MAF package-level / MAF adapter-level / process runtime-level / template/UI-level.
- Note whether this subbundle changes behavior or only improves proof/documentation.

## Closure criteria

This subbundle is complete only when proof files under `proof/SB09` are updated and downstream subbundles can rely on it.

## Status

- Completed

## Objective

Decide whether the MAF adapter needs cleanup after the capability audit.

## Covered Inputs

- RQ03 adapter-level adoption boundary.

## Prerequisites

- SB02 and SB03 capability checks are complete.

## Exact Source References

- `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/MafAgentRuntime.cs`

## Deliverables

- No adapter refactor was introduced because the bundle's runtime fix belongs in process validation.

## Dependency Impact

- SB17 depends on avoiding unnecessary adapter churn.

## Validation Depth

- Source inspection and targeted runtime tests.

## Implementation Steps

- Review adapter surface.
- Keep changes in process runtime where the defect lives.

## Do Not Do

- Do not move process artifact policy into the MAF adapter.

## Acceptance Checklist

- MAF adapter remains behaviorally stable.

## Proof Required

- Final report row and SB02 reflection proof.

## Browser Validation Logging

- No browser route is affected.

## Progression Gate

- Adapter boundary is stable before process runtime fixes.

## Suggested Agent Prompt

Keep adapter changes out unless the source audit proves a real adapter defect.
