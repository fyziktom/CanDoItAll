# SB04: Refactor metadata and grounding logic after SB01-SB03.

## Objective

Refactor metadata and grounding logic after SB01-SB03.

## Why This Matters

This subbundle closes a concrete runtime correctness gap observed after phase5. The process runtime must avoid both false completion and unnecessary blocking while staying generic.

## Implementation Tasks

- Extract `ProcessStepOperationContractResolver`.
- Extract `ProcessTargetGroundingLedgerBuilder`.
- Extract `ProcessInvocationMetadataBuilder`.
- Move tests from reflection-heavy calls toward direct unit tests for extracted services.
- Update architecture documentation and source assertions.

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

- RN09 add refactoring checkpoints every few subbundles.
- RQ06 authoritative grounding ledger.
- RQ11 refactoring checkpoints.

## Prerequisites

- SB01, SB02, and SB03 closure gates pass.
- Architecture notes reflect any source-level boundary changes.

## Exact Source References

- repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ExecutionMetadata.cs
- repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Grounding.cs
- repo://src/CanDoItAll.Modules.Processes/Definitions/ProcessStepOperationContractState.cs
- repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs
- repo://codex/bundles/processes-hardening-followup-runtime-correctness-v6/architecture/02-refactoring-checkpoints.md

## Deliverables

- Extracted metadata/contract/grounding services where the existing partial class is carrying cohesive logic.
- Tests that target extracted services without reflection-heavy invocation where practical.
- Architecture note update for the new boundaries.

## Dependency Impact

- SB05 and SB06 rely on metadata and grounding logic being stable and testable.
- Later policy extraction should not need to rediscover operation contract behavior.

## Validation Depth

- Focused tests for metadata and grounding behavior after extraction.
- Source assertions that extracted services are production-callable, not test-only wrappers.
- Anti-stub audit for unused or duplicate extraction.

## Implementation Steps

- Identify cohesive methods for operation contracts, ledger building, and invocation metadata.
- Extract minimal services or internal helpers following existing DI/test patterns.
- Redirect production dispatch code to the extracted services.
- Move or add tests to target extracted services directly where practical.
- Record proof under `bundle://proof/SB04/`.

## Do Not Do

- Do not perform broad unrelated dispatch refactors.
- Do not change runtime behavior outside the SB01-SB03 stabilized surfaces.
- Do not add interfaces with one trivial implementation unless needed for a real boundary or testing.

## Acceptance Checklist

- Metadata and grounding behavior remains covered by tests.
- Dispatch partial classes are not larger because of checkpoint work.
- Architecture notes and proof cite the extracted boundaries.
- Focused tests pass.

## Proof Required

- `bundle://proof/SB04/manifest.md`
- `bundle://proof/SB04/semantic-invariants.md`
- Passing focused test transcript.
- Source assertion transcript.
- Changed-file SHA-256 transcript.
- Anti-stub audit transcript.

## Browser Validation Logging

- N/A: SB04 is a non-UI refactoring checkpoint.

## Progression Gate

- SB05 may start only after checkpoint A proves metadata/grounding extraction did not weaken SB01-SB03 behavior.

## Suggested Agent Prompt

- Execute checkpoint A with minimal extraction, update architecture proof, rerun focused tests, and record SB04 gate closure.
