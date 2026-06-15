# SB16 Role Editor, Role Templates, Executor Model, And Step Role Bindings

## Status

Future implementation package; prepared by architecture bundle v3; not executed in v3.

## Objective

Rebuild role authoring, role template application, executor preferences, fallback/approval settings, staffing fields, and step role binding foundations over typed role and template contracts.

## Covered Inputs

- REQ-005, REQ-011, REQ-033, REQ-034, REQ-051, REQ-052.
- US-009, US-010, and US-016.
- AC-003, AC-024, AC-030, AC-039, AC-040.

## Prerequisites

- SB15 definition editor complete.
- SB04 template component/override model available.

## Exact Source References

- `repo://codex/bundles/process-module-rewrite-reference-v1/legacy/src/CanDoItAll.Modules.Processes/Components/ProcessRoleEditorForm.razor`
- `repo://codex/bundles/process-module-rewrite-reference-v1/legacy/src/CanDoItAll.Modules.Processes/Components/ProcessStepRoleAssignmentEditor.razor`
- `repo://codex/bundles/process-module-rewrite-reference-v1/legacy/tests/CanDoItAll.Tests.Components/ProcessRoleEditorFormTests.cs`
- `repo://codex/bundles/process-module-architecture-v3/architecture/09-template-git-versioning-and-migrations.md`

## Target Projects / Files

- `src/CanDoItAll.Modules.Processes`
- New Process application/projection projects introduced by upstream backend subbundles.
- `tests/CanDoItAll.Tests.Components`
- `tests/CanDoItAll.Tests.Playwright`
- Subbundle proof directory for screenshots, snapshots, and execution report artifacts.

## Deliverables

- Role list/details UI over role projections.
- Role editor commands for identity, purpose, executor preference, workflow preference, fallback, approval, allocation, and template source.
- Role template apply/customize flow with local override metadata.
- Step role binding command/projection foundation for SB18.

## Dependency Impact

- SB18 uses step role binding contracts.
- SB21 uses executor and staffing fields for launch planning.

## Validation Depth

- Unit tests for role validation and template override merge rules.
- Component tests for role editor, template apply, fallback/approval toggles, and step role binding projection.
- Playwright proof for adding/editing a role and applying a role template.

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

1. Implement role projection and edit command usage in the UI.
2. Rebuild role list, selected role, and add/edit flows.
3. Add role template selection and local override metadata display.
4. Add typed executor preference and workflow preference handling.
5. Expose step role binding projection/command foundations.
6. Add tests and story coverage.

## Do Not Do

- Do not store executor kind as free-form UI strings.
- Do not silently drop template override conflict metadata.
- Do not implement launch candidate matching in this bundle.

## Stop And Report Conditions

- Stop if required projection fields are missing and would force direct runtime or persistence access from UI.
- Stop if preserving the current UX requires reviving old dispatcher/runtime behavior.
- Stop if browser proof cannot be captured for an owned browser-facing story.
- Stop if a story appears to require removal or major UX replacement without explicit user approval.

## Acceptance Checklist

- [ ] Role editor uses typed role and executor models.
- [ ] Role template apply/customize flow records override metadata.
- [ ] Step role binding foundation exists for SB18.
- [ ] Component and Playwright proof exists.

## Proof Required

- Unit/component test output.
- Playwright role/template screenshot evidence.
- Story coverage table for US-009, US-010, and US-016.

## Browser Validation Logging

- Required. Capture role tab route/state, add/edit/apply-template actions, assertions, screenshot, and console/network summary.

## Progression Gate

- SB17 may start after role projection contracts and role editing commands are stable.

## Suggested Agent Prompt

Execute SB16 from `codex/bundles/process-module-architecture-v3/subbundles/16-role-editor-role-templates-executor-model-and-step-role-bindings`. Rebuild role authoring and template customization over typed contracts.

## Handoff Notes For Next Bundle

Record canvas role-binding needs and launch role-resolution fields for downstream bundles.
