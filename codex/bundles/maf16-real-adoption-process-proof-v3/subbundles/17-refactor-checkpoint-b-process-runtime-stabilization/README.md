# SB17: 17-refactor-checkpoint-b-process-runtime-stabilization

## Goal

Stabilize runtime code after fixes.

## Required work

- Refactor duplicated path/content/hash logic.
- Document service boundaries.
- Run build and focused tests.
- Update skills/docs if behavior changes.

## Required proof

- Failing-first or adversarial proof.
- Passing production-path proof.
- Source assertions with exact repo paths.
- Anti-stub audit.
- Changed-file hashes.
- Classification: MAF package-level / MAF adapter-level / process runtime-level / template/UI-level.
- Note whether this subbundle changes behavior or only improves proof/documentation.

## Closure criteria

This subbundle is complete only when proof files under `proof/SB17` are updated and downstream subbundles can rely on it.

## Status

- Completed

## Objective

Stabilize the process runtime after artifact validation and read-model fixes.

## Covered Inputs

- RQ09 and RQ10 runtime stabilization.

## Prerequisites

- SB11 and SB13 implementation tests pass.

## Exact Source References

- `repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessesService.RuntimeReadQuery.Support.cs`

## Deliverables

- Small scoped runtime changes without broad refactor.

## Dependency Impact

- SB18 depends on the runtime change set being reviewable.

## Validation Depth

- Focused integration tests and source audit.

## Implementation Steps

- Keep helpers private and typed.
- Avoid extracting a service without real duplication pressure.

## Do Not Do

- Do not refactor unrelated runtime flows.

## Acceptance Checklist

- Runtime changes are scoped to validation diagnostics and status projection.

## Proof Required

- SB11 and SB13 proof manifests plus final report.

## Browser Validation Logging

- No browser route is affected.

## Progression Gate

- Stabilization must finish before final closure.

## Suggested Agent Prompt

Review the runtime diff for scope and keep only the helpers needed by the tests.
