# SB12: Define strict vs compatibility contract modes.

## Objective

Define strict vs compatibility contract modes.

## Why This Matters

This subbundle closes a concrete runtime correctness gap observed after phase5. The process runtime must avoid both false completion and unnecessary blocking while staying generic.

## Implementation Tasks

- Add contract strictness policy for process definition versions.
- New or edited definitions should require explicit operation contract for risky steps.
- Legacy/migrated definitions can run in compatibility mode with visible warnings.
- Allow strict mode to be enforced on publish/run-start by criticality/autonomy.
- Add migration/template update tests.

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

- RQ09 contract strictness.
- RN09 add refactoring checkpoints every few subbundles.

## Prerequisites

- SB11 closure gate passes.
- Operation contract resolver behavior remains stable.

## Exact Source References

- repo://src/CanDoItAll.Modules.Processes/Definitions/ProcessDefinitionLinter.cs
- repo://src/CanDoItAll.Modules.Processes/Definitions/ProcessStepOperationContractState.cs
- repo://src/CanDoItAll.Modules.Processes/Services/ProcessesService.Support.cs
- repo://src/CanDoItAll.Modules.Processes/Services/ProcessesService.Publication.cs
- repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessesService.Runtime.RunStart.cs
- repo://tests/CanDoItAll.Tests.Integration/ProcessDefinitionLinterTests.cs

## Deliverables

- Strict vs compatibility lint mode policy for process definitions.
- New or edited risky definitions require explicit operation contracts.
- Legacy/migrated definitions run with compatibility warnings unless strict mode is required by criticality/autonomy.
- Template/migration coverage for explicit operation contracts.

## Dependency Impact

- SB13 diagnostics display strictness warnings and invariant issues.
- SB14 final red-team validates strict and compatibility behavior across process types.

## Validation Depth

- Linter tests for strict failures and compatibility warnings.
- Publish/run-start tests for enforced strict mode by criticality or autonomy.
- Source assertions for strictness gate call sites.

## Implementation Steps

- Review existing linter strict mode and fill missing contract gates.
- Enforce strict mode for new/edited risky definitions and high-risk publish/run-start paths.
- Preserve legacy compatibility mode with visible warnings.
- Update templates or migration helpers to include explicit operation contracts.
- Record proof under `bundle://proof/SB12/`.

## Do Not Do

- Do not silently infer risky operations for new strict definitions.
- Do not make legacy compatibility hide errors without warnings.
- Do not add domain-specific contract requirements.

## Acceptance Checklist

- [x] Strict mode fails missing risky operation contracts.
- [x] Compatibility mode warns without blocking allowed legacy cases.
- [x] Publish/run-start gates enforce strictness when required.
- [x] Focused linter/integration tests pass.

## Closure Notes

- Added persisted `ProcessDefinitionContractMode` on process definition versions.
- Existing rows migrate to `Compatibility`; new/editor/template versions can be strict.
- Publish and run-start gates enforce strictness from request mode, version mode, criticality, or autonomy.
- Migrated Blazor templates to typed operation contracts and strict-compatible artifact recovery policy text.

## Proof Required

- `bundle://proof/SB12/manifest.md`
- `bundle://proof/SB12/semantic-invariants.md`
- Failing-first or red-team transcript.
- Passing focused test transcript.
- Changed-file SHA-256 transcript.
- Anti-stub audit transcript.

## Browser Validation Logging

- N/A: SB12 changes definition validation and templates only.

## Progression Gate

- SB13 may start only after strictness gates are explicit and compatibility warnings are visible.

## Suggested Agent Prompt

- Implement SB12 strictness gates and template migration, update `proof/SB12`, run linter tests, and record gate closure.
