# SB15 Definition Editor, Governance, Contracts, Simulation, Lint, And Publication

## Status

Future implementation package; prepared by architecture bundle v3; not executed in v3.

## Objective

Rebuild the definition editor for identity, governance, contracts, simulation readiness, linting, save, publish, archive/delete, and validation feedback through typed command and projection contracts.

## Covered Inputs

- REQ-001, REQ-005, REQ-024, REQ-030, REQ-051, REQ-052.
- US-005 through US-008.
- AC-003, AC-012, AC-021, AC-035, AC-039, AC-040.

## Prerequisites

- SB14 definition catalog and selection complete.
- Core definition draft/status contracts available from SB03.

## Exact Source References

- `repo://src/CanDoItAll.Modules.Processes/Components/ProcessDefinitionForm.razor`
- `repo://tests/CanDoItAll.Tests.Components/ProcessDefinitionFormTests.cs`
- `repo://tests/CanDoItAll.Tests.Integration/ProcessDefinitionLinterTests.cs`
- `repo://codex/bundles/process-module-architecture-v3/architecture/03-core-model-and-invariants.md`

## Target Projects / Files

- `src/CanDoItAll.Modules.Processes`
- New Process application/projection projects introduced by upstream backend subbundles.
- `tests/CanDoItAll.Tests.Components`
- `tests/CanDoItAll.Tests.Playwright`
- Subbundle proof directory for screenshots, snapshots, and execution report artifacts.

## Deliverables

- Projection-backed definition editor with Identity, Governance, Contracts, and Simulation sections.
- Typed save, publish, archive/delete command handlers.
- Lint result projection with blocking and warning states.
- Component tests and Playwright edit/publish proof.

## Dependency Impact

- SB16, SB17, SB18, SB19, and launch/runtime bundles rely on correct definition identity/status/governance data.

## Validation Depth

- Unit tests for definition status transitions and lint severity handling.
- Component tests for validation, disabled actions, error display, and command receipts.
- Playwright proof for editing and publishing a draft definition.

## Refactoring Review Checkpoint

- Keep component rendering separate from projection loading and command dispatch.
- Keep projection client code out of low-level visual components.
- Split large components or services before handoff if they combine unrelated workflow areas.
- Verify UI code does not reference runtime internals, EF runtime entities, or old observation services.

## Implementation Steps

1. Bind editor to definition draft projection and typed edit commands.
2. Implement identity and governance field updates with explicit validation.
3. Render contract and simulation readiness summaries from projections.
4. Wire save, publish, and archive/delete commands with command receipts.
5. Add lint projection display and blocking behavior.
6. Add tests and story coverage for US-005 through US-008.

## Do Not Do

- Do not mutate EF entities directly from UI.
- Do not hide publish failures behind silent fallback.
- Do not implement role, step, launch, or runtime editing in this bundle.

## Stop And Report Conditions

- Stop if required projection fields are missing and would force direct runtime or persistence access from UI.
- Stop if preserving the current UX requires reviving old dispatcher/runtime behavior.
- Stop if browser proof cannot be captured for an owned browser-facing story.
- Stop if a story appears to require removal or major UX replacement without explicit user approval.

## Acceptance Checklist

- [ ] Identity, governance, contracts, and simulation sections render from projections.
- [ ] Save/publish/archive/delete use typed commands.
- [ ] Lint warnings/errors are visible and actionable.
- [ ] Component and Playwright proof exists.

## Proof Required

- Unit/component test output.
- Playwright edit/publish screenshot evidence.
- Story coverage table for US-005 through US-008.

## Browser Validation Logging

- Required. Capture route, selected definition, edited fields, save/publish action, visible result, screenshot, and console/network summary.

## Progression Gate

- SB16 may start after definition edit commands and lint projection behavior are proven.

## Suggested Agent Prompt

Execute SB15 from `codex/bundles/process-module-architecture-v3/subbundles/15-definition-editor-governance-contracts-simulation-lint-and-publication`. Rebuild only definition authoring and publication behavior over typed projections/commands.

## Handoff Notes For Next Bundle

Record role editor projection fields still missing and any governance policy fields SB21/SB24 must consume.
