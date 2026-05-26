# 01-template-vocabulary-and-ui-option-parity

## Status

- `Completed`

## Objective

- Make process role and step-definition authoring options complete for the current template-pack vocabulary without narrowing or silently falling back.

## Success Criteria

- Role executor UI preserves `person`, `agent`, `person-or-agent`, `AI agent`, and workflow intent.
- Step role assignment UI exposes all `ProcessResponsibilityKind` values, including `Accountable`.
- Artifact expectation UI exposes all `ProcessArtifactKind` and `ProcessArtifactTrustRequirement` values, including `DecisionRecord` and `ApprovalRequired`.
- Template projection maps current process template vocabulary into supported typed options without fallback for owned fields.
- Focused component/integration tests pass.

## Covered Inputs

- N001
- R001, R002, R003, R004

## Prerequisites

- Prepared-stage bundle validator passes.
- Current source references still exist.

## Exact Source References

- `repo://src/CanDoItAll.Modules.Processes/Definitions/ProcessDefinitionEnums.cs`
- `repo://src/CanDoItAll.Modules.Processes/Definitions/ProcessDefinitionEditorModels.cs`
- `repo://src/CanDoItAll.Modules.Processes/Components/ProcessRoleEditorForm.razor`
- `repo://src/CanDoItAll.Modules.Processes/Components/ProcessStepRoleAssignmentEditor.razor`
- `repo://src/CanDoItAll.Modules.Processes/Components/ProcessArtifactExpectationEditor.razor`
- `repo://src/CanDoItAll.Modules.Processes/Templates/ProcessTemplateEditorModelFactory.cs`
- `repo://src/CanDoItAll.Modules.Processes/Templates/ProcessTemplateProjectionService.cs`
- `repo://src/CanDoItAll.Modules.Processes/Canvas/ProcessCanvasCatalog.cs`
- `repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessesService.Runtime.RunStart.cs`
- `repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessesService.RuntimeReadQuery.Support.cs`
- `repo://Templates/Processes`
- `repo://tests/CanDoItAll.Tests.Components`
- `repo://tests/CanDoItAll.Tests.Integration/ProcessTemplateGovernanceTests.cs`

## Deliverables

- Strongly typed role executor option catalog or equivalent helper.
- Added missing domain enum values and downstream switch handling.
- Updated Blazor forms using existing component patterns.
- Tests for UI rendering, model updates, and template vocabulary drift.

## Dependency Impact

- SB02 depends on this phase because reloaded definitions must preserve current template vocabulary.
- Runtime assignment, canvas ports, and artifact validation depend on the new enum values not being UI-only.

## Validation Depth

- Critical UI and domain-contract foundation.
- Requires Semantic Adequacy Gate proof with shallow-pass trap, adversarial negative proof, semantic positive proof, anti-stub audit, and raw-note literal closure.

## Implementation Steps

1. Add a typed executor option helper and update `ProcessRoleEditorForm` to render/persist all supported executor choices.
2. Add `Accountable`, `DecisionRecord`, and `ApprovalRequired` where they represent first-class process semantics.
3. Update canvas/runtime/trust switch expressions so new values behave predictably.
4. Add component tests for role executor options, responsibility options, and artifact options.
5. Add or update template governance tests so current template vocabulary cannot silently fall back.
6. Run focused tests and build/source audits.

## Scope Exceptions

- None planned.

## Do Not Do

- Do not add arbitrary free-text option entry to the UI.
- Do not change unrelated agent, project, memory, or workflow settings.
- Do not rewrite the process editor layout.
- Do not normalize `person-or-agent` to `AI agent`.

## Acceptance Checklist

- [x] Existing template vocabulary is enumerated and either supported or explicitly rejected by tests.
- [x] Role executor dropdown includes supported role executor choices and round-trips them.
- [x] Step role assignment dropdown includes `Accountable`.
- [x] Artifact expectation dropdowns include `DecisionRecord` and `ApprovalRequired`.
- [x] Projection tests prove no fallback for owned missing values.
- [x] Build/focused tests pass.

## Proof Required

- `proof/SB01/manifest.md`
- `proof/SB01/semantic-invariants.md`
- `bundle://proof/SB01/manifest.md`
- `bundle://proof/SB01/semantic-invariants.md`
- Changed-file hashes transcript.
- Failing-first or source assertion showing missing vocabulary before implementation.
- Passing component/integration test transcripts.
- Anti-stub audit transcript.
- Browser proof on the process editor route if local server startup succeeds.

## Browser Validation Logging

- Route: `/processes` or the project-specific process page if that is the available local route.
- Viewport: large desktop first; narrow follow-up only if layout changed.
- Actions: open a definition editor, inspect role executor dropdown, inspect step role assignment responsibility dropdown, inspect artifact kind/trust dropdowns.
- Screenshots: `evidence/SB01/process-role-options-desktop.png`, `evidence/SB01/process-step-options-desktop.png`.
- Review questions: options are readable, dropdowns are not clipped, labels fit, and existing form alignment is preserved.

## Progression Gate

- SB02 may start only after SB01 proof shows no unsupported current template vocabulary for owned fields and focused tests pass.

## Suggested Agent Prompt

```text
Implement this subbundle only.
Work outcome-first: preserve the listed scope boundaries, verify prerequisites before editing, make the smallest correct change set, capture the required proof, update the execution report rows, and stop if the progression gate cannot honestly pass.
```
