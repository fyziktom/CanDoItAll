# SB18 Step Editor, Operation Contracts, Routing, Artifacts, And Subprocess Mapping

## Status

Completed in the approved implementation pass. Proof is recorded under `bundle://proof/SB18/`.

## Objective

Rebuild step authoring for basic info, execution strategy inputs, operation contracts, contracts, branch routing, role assignments, artifact expectations, and subprocess mappings.

## Covered Inputs

- REQ-004, REQ-008, REQ-011 to REQ-018, REQ-042 to REQ-045, REQ-051, REQ-052.
- US-011 through US-017.
- AC-004, AC-005, AC-011, AC-014, AC-033, AC-039, AC-040.

## Prerequisites

- SB17 canvas selection and command routing complete.
- SB09 branch/subprocess manager contracts complete.

## Exact Source References

- `repo://codex/bundles/process-module-rewrite-reference-v1/legacy/src/CanDoItAll.Modules.Processes/Components/ProcessStepEditorForm.razor`
- `repo://codex/bundles/process-module-rewrite-reference-v1/legacy/src/CanDoItAll.Modules.Processes/Components/ProcessArtifactExpectationEditor.razor`
- `repo://codex/bundles/process-module-rewrite-reference-v1/legacy/src/CanDoItAll.Modules.Processes/Components/ProcessStepBranchOutcomeEditor.razor`
- `repo://codex/bundles/process-module-rewrite-reference-v1/legacy/tests/CanDoItAll.Tests.Playwright/AppSmokeTests.ProcessOperationContract.cs`
- `repo://codex/bundles/process-module-rewrite-reference-v1/legacy/tests/CanDoItAll.Tests.Integration/ProcessSubprocessIntegrationTests.cs`

## Target Projects / Files

- `src/CanDoItAll.Modules.Processes`
- New Process application/projection projects introduced by upstream backend subbundles.
- `tests/CanDoItAll.Tests.Components`
- `tests/CanDoItAll.Tests.Playwright`
- Subbundle proof directory for screenshots, snapshots, and execution report artifacts.

## Deliverables

- Step editor UI over typed step projections and commands.
- Operation contract editor using strongly typed operations and target scopes.
- Branch outcome editor using typed route targets and loop budget metadata.
- Artifact expectation editor with trust, sensitivity, retention, workflow output, child artifact, future usage, and validation summaries.
- Subprocess definition mapping and child artifact reference support.

## Dependency Impact

- SB21 launch planning depends on complete step/role/artifact definitions.
- SB23 runtime canvas depends on subprocess and branch metadata.

## Validation Depth

- Unit tests for operation contract, branch route, artifact expectation, and subprocess validation.
- Component tests for each step editor tab.
- Playwright proof for editing a step, adding branch outcome, adding artifact expectation, and subprocess mapping.

## Refactoring Review Checkpoint

- Keep component rendering separate from projection loading and command dispatch.
- Keep projection client code out of low-level visual components.
- Split large components or services before handoff if they combine unrelated workflow areas.
- Verify UI code does not reference runtime internals, EF runtime entities, or old observation services.

## Performance Antipattern Notes

- Read `architecture/19-dotnet-performance-guardrails.md` and `validation/05-dotnet-performance-antipattern-checklist.md` before creating or modifying C# hot-path code.
- Record exact performance scan counts in the execution report when this subbundle changes runtime, dispatcher, manager, projection, template, Git, adapter, persistence, or UI service code.
- Do not introduce sync-over-async, unbounded event/projector queues, per-call `HttpClient`, per-call `JsonSerializerOptions`, load-all UI queries, or LINQ-heavy hot paths without a recorded mitigation and proof.
## Implementation Steps

1. Bind Basic, Execution, Contracts, Routing, Roles, and Artifacts sections to typed projections.
2. Replace free-form operation/routing state with typed enums and route target values.
3. Wire artifact expectation commands to artifact slot contracts.
4. Wire subprocess mapping commands to builder-compatible contracts.
5. Add validation, component tests, and Playwright proof.
6. Record story coverage for US-011 through US-017.

## Do Not Do

- Do not implement free-text branch token routing.
- Do not store operation contracts as unstructured UI text.
- Do not let subprocess references bypass builder compatibility checks.

## Stop And Report Conditions

- Stop if required projection fields are missing and would force direct runtime or persistence access from UI.
- Stop if preserving the current UX requires reviving old dispatcher/runtime behavior.
- Stop if browser proof cannot be captured for an owned browser-facing story.
- Stop if a story appears to require removal or major UX replacement without explicit user approval.

## Acceptance Checklist

- [x] Step editor covers basic, execution, contracts, routing, roles, and artifacts.
- [x] Branch outcomes are typed and loop-aware.
- [x] Artifact expectations include trust/sensitivity/retention/provenance fields.
- [x] Subprocess mapping is builder-compatible.
- [x] Component and Playwright proof exists.

## Implementation Result

- Added `ProcessDefinitionStepEditorProjection` contracts, typed step command DTOs, operation/route/artifact/subprocess enums, command receipts, and lint projections.
- Added `ProcessDefinitionStepEditorProjectionService` to build template-backed step drafts, execute save/add-branch/add-artifact/map-subprocess commands, reject stale versions, require explicit operation target scope, require loop budgets for backward routes, and preserve subprocess mapping metadata.
- Added `ProcessDefinitionStepEditorPanel.razor` and shell/client/DI wiring so the UI remains projection-first and emits typed commands.
- Extended template loading to expose step authoring defaults for operations, branch routing, artifacts, roles, and subprocess options.
- Added focused unit, component, and Playwright proof for US-011 through US-017.

## Proof

- `bundle://proof/SB18/manifest.md`
- `bundle://proof/SB18/semantic-invariants.md`
- `bundle://proof/SB18/red-team-semantic-proof.md`
- `bundle://proof/SB18/story-coverage.md`
- `bundle://proof/SB18/browser-validation.md`
- `bundle://proof/SB18/test-unit-step-editor-sb18.txt`
- `bundle://proof/SB18/test-components-process-shell-sb18.txt`
- `bundle://proof/SB18/test-playwright-process-shell-sb18.txt`
- `bundle://proof/SB18/browser/processes-definition-step-editor.png`

## Proof Required

- Unit/component/E2E test output.
- Playwright step editor screenshot evidence.
- Story coverage table for US-011 through US-017.

## Browser Validation Logging

- Required. Capture selected step, edited tab actions, assertions, screenshots, and console/network summary.

## Progression Gate

- SB19 and SB21 may start after step, branch, artifact, and subprocess authoring contracts are proven.

## Suggested Agent Prompt

Execute SB18 from `codex/bundles/process-module-architecture-v3/subbundles/18-step-editor-operation-contracts-routing-artifacts-and-subprocess-mapping`. Rebuild step authoring over typed contracts and reject free-text routing.

## Handoff Notes For Next Bundle

Record template import target-step needs and launch-readiness validation fields.
