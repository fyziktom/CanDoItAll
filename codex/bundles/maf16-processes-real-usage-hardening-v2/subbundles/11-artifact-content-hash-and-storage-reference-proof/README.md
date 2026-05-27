# SB11: Artifact Content Hash And Storage Reference Proof

## Status

- Completed

## Objective

Close content hash and storage-reference semantics for required process artifacts.

## Covered Inputs

- RQ05: prove artifact content hash and storage reference behavior.

## Prerequisites

- SB10 current-run artifact validation must be complete.

## Exact Source References

- repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessesService.Runtime.Operations.cs
- repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.StepCompletionFinalizer.cs
- repo://tests/CanDoItAll.Tests.Integration/ProcessesServiceIntegrationTests.cs

## Deliverables

- Tests proving `RecordArtifactAsync` computes content hash for workspace/storage artifacts.
- Tests proving empty hash or unreadable content does not satisfy required evidence.

## Dependency Impact

- SB12 parity and SB13 recovery correctness depend on hash and storage-reference truth.

## Validation Depth

- Critical semantic proof must include storage reference, organization-scoped path, and plain run-scoped path cases.

## Implementation Steps

- Audit content-hash computation and lineage JSON.
- Add or update tests for storage reference and workspace path variants.
- Fix production logic if empty hashes are treated as success.
- Update `proof/SB11`.

## Do Not Do

- Do not make `ContentHash` optional for required evidence without explicit unavailable status.
- Do not deduplicate across wrong step/expectation lineage.

## Acceptance Checklist

- Content hash is computed/preserved where content is readable.
- Hash mismatch and content unavailable are rejected distinctly.
- Tests cover all required path variants.

## Proof Required

- Failing-first transcript for empty/mismatched hash.
- Passing integration transcript.
- Source assertions, anti-stub audit, and hashes.

## Browser Validation Logging

- N/A - no browser-visible behavior in this subbundle.

## Progression Gate

- SB12 may start only after content/hash semantics are proven.

## Suggested Agent Prompt

Prove content-hash and storage-reference semantics for required process artifacts and prevent unreadable/empty-hash false satisfaction.

## Closure Proof

- bundle://proof/SB11/manifest.md
- bundle://proof/SB11/semantic-invariants.md

