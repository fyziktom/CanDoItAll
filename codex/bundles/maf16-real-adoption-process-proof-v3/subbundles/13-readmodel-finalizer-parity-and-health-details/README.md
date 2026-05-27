# SB13: 13-readmodel-finalizer-parity-and-health-details

## Goal

Make step detail health match finalizer validation.

## Required work

- Step detail artifact satisfaction should expose exact validation status, not only Satisfied/Missing.
- Run health missing/invalid artifact counts should match validation results.
- Add tests where an artifact is recorded but invalid and the UI/API does not show it as fully satisfied.

## Required proof

- Failing-first or adversarial proof.
- Passing production-path proof.
- Source assertions with exact repo paths.
- Anti-stub audit.
- Changed-file hashes.
- Classification: MAF package-level / MAF adapter-level / process runtime-level / template/UI-level.
- Note whether this subbundle changes behavior or only improves proof/documentation.

## Closure criteria

This subbundle is complete only when proof files under `proof/SB13` are updated and downstream subbundles can rely on it.

## Status

- Completed

## Objective

Expose finalizer content-unavailable diagnostics through operator obligations and run health.

## Covered Inputs

- RQ09 read-model, API, UI, and recovery semantics.

## Prerequisites

- SB11 content policy emits a typed diagnostic.

## Exact Source References

- `repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessesService.RuntimeReadQuery.cs`
- `repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessesService.RuntimeReadQuery.Support.cs`
- `repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessHealthInvariantAuditor.cs`
- `repo://tests/CanDoItAll.Tests.Integration/ProcessRuntimeOperatorReadModelTests.cs`

## Deliverables

- Operator read model reports `ContentUnavailable`; health counts it as missing artifact risk.

## Dependency Impact

- SB15 and SB18 depend on honest operator status.

## Validation Depth

- Integration test with recorded artifact and matching validation diagnostic.

## Implementation Steps

- Load artifact validation diagnostics for step runs.
- Match diagnostics to artifact obligations.
- Map `ContentUnavailable` through health and UI tone surfaces.

## Do Not Do

- Do not fix this only in the UI.

## Acceptance Checklist

- Content-unavailable artifacts are not displayed as satisfied.

## Proof Required

- `proof/SB13/manifest.md` and `proof/SB13/semantic-invariants.md`.

## Browser Validation Logging

- No browser route is affected.

## Progression Gate

- Read-model parity must pass before release readiness.

## Suggested Agent Prompt

Project finalizer artifact diagnostics into the read model and health auditor with typed status.
