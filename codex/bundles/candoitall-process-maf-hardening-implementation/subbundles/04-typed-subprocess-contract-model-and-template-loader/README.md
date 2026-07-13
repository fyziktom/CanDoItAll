# SB04 - Typed Subprocess Contract Model And Template Loader

## Status

- `Completed`
- Critical foundation: yes

## Objective

Introduce typed subprocess contract metadata and loader validation so accepted, repaired, no-go, manual-skip, materialization, and required receipt rules stop living only in prose.

## Covered Inputs

- F03, F04, F09, F11.
- R07, R08, R09, R11, R12, R13.
- GPTPro B02 and B06.

## Prerequisites

- SB01 complete.

## Exact Source References

- `repo://src/Processes/CanDoItAll.Processes.Templates/ProcessTemplatePackLoader.cs`
- `repo://src/Processes/CanDoItAll.Processes.Templates/ProcessTemplateStepSummaries.cs`
- `repo://src/Processes/CanDoItAll.Processes.Application/ProcessLaunchApplicationService.cs`
- `repo://src/Processes/CanDoItAll.Processes.Application/ProcessStepBriefContracts.cs`
- `repo://Templates/Processes/processes/dotnet-development-slice/definition.json`
- `repo://Templates/Processes/processes/software-delivery/definition.json`
- `repo://Templates/Processes/processes/dotnet-solution-setup/definition.json`
- `repo://Templates/Processes/processes/dotnet-feature-function-implementation/definition.json`
- `repo://Templates/Processes/processes/dotnet-ui-screenshot-writeback/definition.json`
- `repo://Templates/Processes/processes/dotnet-runtime-command-writeback/definition.json`

## Deliverables

- Strongly typed template document records for `SubprocessContract`.
- Runtime-consumable contract model with launch mode, parent expectation, accepted child outputs, no-go child outputs, required child receipts, already-satisfied output, and materialization mode.
- Template loader validation rules.
- Compatibility mapping from legacy `SubprocessChildStepKey` fields when typed metadata is absent.
- Summary/projection support for typed contracts.
- Tests for all validation rules.

## Dependency Impact

- SB05 depends on this typed model to avoid generic child evidence. SB08 depends on it to update templates safely.

## Validation Depth

- Critical foundation.
- Requires semantic negative tests for shallow metadata.

## Implementation Steps

1. Decide exact project placement for template document records and runtime contract records.
2. Add typed JSON-loadable document records.
3. Add validator rules:
   - subprocess step with `SubprocessProcessKey` must have typed contract or accepted compatibility contract;
   - parent produced artifact expectation key must exist;
   - accepted and no-go child outputs must not overlap;
   - required output manual skip needs typed already-satisfied output;
   - launch mode must be known enum;
   - child output rows must include step key and artifact expectation key/title.
4. Add summaries so UI/editor/projection can display typed contract without parsing markdown.
5. Add compatibility behavior for existing templates before SB08 edits every file.
6. Add tests for `prepare-solution-skeleton` and representative software-delivery parents.

## Scope Exceptions

- Do not hard-edit every process template in SB04; SB08 owns migration across templates after runtime support exists.
- Do not implement bridge execution in this phase; SB05 owns it.

## Do Not Do

- Do not use raw dictionaries or unvalidated JSON objects in runtime behavior.
- Do not infer accepted/no-go outputs by brittle markdown scanning.
- Do not add .NET-specific child branch names to generic contracts.

## Acceptance Checklist

- [ ] Typed contract loads from JSON.
- [ ] Legacy fields still load for backward compatibility.
- [ ] Manual skip on required output without typed proof fails validation.
- [ ] Accepted/no-go overlap fails validation.
- [ ] Missing parent expectation key fails validation.
- [ ] Summary/projection exposes typed accepted/no-go rows.

## Proof Required

- `proof/SB04/manifest.md`
- `proof/SB04/semantic-invariants.md`
- Failing-first tests for missing typed contract/manual skip.
- Passing template validation tests.
- Source assertions that typed records are strongly typed.
- Changed-file hashes.
- Anti-stub audit.
- Production Behavior Artifact Matrix for `SubprocessContract`.

## Browser Validation Logging

- `N/A` unless template editor UI rendering changes.

## Progression Gate

- SB05 cannot start until typed contract parser/validator passes and can represent SP01-SP09 from `inventories/02-subprocess-contract-inventory.md`.

## C# Architecture Impact

Adds typed contract model and validation boundary.

## Boundary Ownership

Template document shape stays in templates. Runtime/application consume validated model through contracts/abstractions.

## Dependency Direction

Do not make runtime depend on template loader implementation. Use normalized contract transfer.

## Pattern Decision

Builder/validator pattern for contract construction; strongly typed enums for launch/materialization modes.

## Testability Contract

Template validation tests run without full app host and without live templates beyond fixture files.

## Partial Class Policy

Do not expand `ProcessTemplatePackLoader` with a giant validation method. Extract focused validator if needed.

## Architecture Proof Required

- Project placement rationale.
- Source assertion for focused validator.
- CodeAnalytics dependency check if project references change.

## Suggested Agent Prompt

```text
Execute SB04 only. Add typed subprocess contract records and template validation. Preserve compatibility. Do not implement bridge execution or template migration beyond fixtures needed for tests.
```
