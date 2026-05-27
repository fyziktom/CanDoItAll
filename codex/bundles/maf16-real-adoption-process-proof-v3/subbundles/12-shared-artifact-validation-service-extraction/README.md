# SB12: 12-shared-artifact-validation-service-extraction

## Goal

Extract/reuse one artifact validation service across runtime surfaces.

## Required work

- Move validation logic out of dispatch partials if practical.
- Use the same validation service for finalizer, read model, API/manual transition, recovery, and health diagnostics.
- Prevent duplicate partial implementations and divergent semantics.

## Required proof

- Failing-first or adversarial proof.
- Passing production-path proof.
- Source assertions with exact repo paths.
- Anti-stub audit.
- Changed-file hashes.
- Classification: MAF package-level / MAF adapter-level / process runtime-level / template/UI-level.
- Note whether this subbundle changes behavior or only improves proof/documentation.

## Closure criteria

This subbundle is complete only when proof files under `proof/SB12` are updated and downstream subbundles can rely on it.

## Status

- Completed

## Objective

Reuse validation semantics across finalizer and read model without unnecessary abstraction.

## Covered Inputs

- RQ09 shared validation semantics.

## Prerequisites

- SB11 produces a typed validation status and diagnostic.

## Exact Source References

- `repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessesService.RuntimeReadQuery.Support.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.StepCompletionFinalizer.cs`

## Deliverables

- Shared persisted diagnostics consumed by the read model.

## Dependency Impact

- SB13 depends on the finalizer/read-model semantic match.

## Validation Depth

- Integration read-model parity test.

## Implementation Steps

- Persist finalizer validation diagnostics.
- Project matching diagnostics into artifact obligations.

## Do Not Do

- Do not add a new service abstraction unless duplication requires it.

## Acceptance Checklist

- Read model observes finalizer validation diagnostics.

## Proof Required

- SB13 proof manifest and tests.

## Browser Validation Logging

- No browser route is affected.

## Progression Gate

- Diagnostic reuse must work before operator health is trusted.

## Suggested Agent Prompt

Reuse the persisted artifact validation diagnostics and avoid a speculative service extraction.
