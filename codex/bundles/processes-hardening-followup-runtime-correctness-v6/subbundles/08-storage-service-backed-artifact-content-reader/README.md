# SB08: Replace workspace-only artifact content validation with storage abstraction.

## Objective

Replace workspace-only artifact content validation with storage abstraction.

## Why This Matters

This subbundle closes a concrete runtime correctness gap observed after phase5. The process runtime must avoid both false completion and unnecessary blocking while staying generic.

## Implementation Tasks

- Introduce `IProcessArtifactContentReader` backed by storage placement/service driver.
- Keep workspace reader as fallback only for managed workspace paths.
- Validate artifacts stored outside workspace root or via future IPFS/storage drivers.
- Add tests with fake storage reader and workspace reader.
- Ensure finalizer and manual transition use the same reader.

## Required Tests

- Add failing-first or red-team tests before the production fix where practical.
- Add positive tests proving the fixed behavior.
- Include at least one generic/non-software case if this subbundle changes generic process semantics.

## Closure Criteria

- Production code implements the behavior; no prompt-only fix.
- Proof manifest is updated.
- Focused tests pass.
- No SQLite runtime/migration dependency is introduced.

## Status

- Completed

## Covered Inputs

- RN08 rely on workspace filesystem validation instead of the storage abstraction.
- RN02 complete with weak/manual artifact validation.
- RQ08 storage service artifact validation.

## Prerequisites

- SB07 closure gate passes.
- Shared completion validator boundary is stable.

## Exact Source References

- repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.StepCompletionFinalizer.cs
- repo://src/CanDoItAll.Infrastructure/Storage/Abstractions/StorageContracts.cs
- repo://src/CanDoItAll.Infrastructure/Storage/Placement/StoragePlacementService.cs
- repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs

## Deliverables

- Storage-backed `IProcessArtifactContentReader` implementation using storage placement/driver abstractions.
- Workspace reader retained only as managed workspace-path fallback.
- Manual and automated completion validation use the same reader path.

## Dependency Impact

- SB09 artifact projection mapping must validate content through this abstraction.
- SB14 generic scenarios depend on non-workspace artifact validation.

## Validation Depth

- Tests with fake storage-backed artifact content.
- Tests proving workspace fallback still works for managed workspace paths.
- Source assertions that finalizer and manual/API validation use the same reader.

## Implementation Steps

- Introduce or complete storage-backed content reader around storage driver registry or placement services.
- Wire finalizer and manual transition validator through the same reader.
- Add tests for storage-backed and workspace-backed content.
- Remove direct workspace-only assumptions from validation.
- Record proof under `bundle://proof/SB08/`.

## Do Not Do

- Do not add SQLite runtime paths or provider switching.
- Do not bypass storage abstractions with absolute filesystem reads except managed workspace fallback.
- Do not leave manual/API validation on a different reader than automated finalizer.

## Acceptance Checklist

- Storage-backed artifacts outside the workspace can be validated.
- Workspace-managed artifacts still validate through fallback.
- Malformed or missing storage content is rejected consistently.
- Focused tests pass.

## Proof Required

- `bundle://proof/SB08/manifest.md`
- `bundle://proof/SB08/semantic-invariants.md`
- Failing-first or red-team transcript.
- Passing focused test transcript.
- Changed-file SHA-256 transcript.
- Anti-stub audit transcript.

## Browser Validation Logging

- N/A: SB08 changes storage/runtime validation only.

## Progression Gate

- Closed. SB09 may start because storage-backed artifact validation is proven through the shared validator for both manual/API and automated completion paths.

## Completion Notes

- Manual/API completion now creates the same storage-backed managed artifact content reader used by the automated finalizer.
- Storage reference JSON is resolved through the storage catalog and driver registry; relative managed workspace paths remain a fallback.
- Focused integration tests prove storage-backed malformed JSON is rejected on manual completion and existing workspace fallback validation still works.
- No SQLite runtime or migration dependency was introduced.

## Suggested Agent Prompt

- Implement SB08 storage-backed artifact content reading, update `proof/SB08`, run focused process tests, and record gate closure.
