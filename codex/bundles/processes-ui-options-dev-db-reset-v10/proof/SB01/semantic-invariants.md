# SB01 Semantic Invariants

## SB01-INV-001

- Invariant ID: `SB01-INV-001`
- Source raw note: `N001`
- Expected behavior: Process role executor UI preserves the current template executor vocabulary, including `person`, `agent`, `person-or-agent`, legacy `AI agent`, and `Workflow`.
- Disallowed shallow implementation: Rendering text labels without persisting the selected executor value, or mapping `person-or-agent` to a narrower agent-only value.
- Failing-first test: `bundle://proof/SB01/transcripts/failing-first.txt`
- Passing test: `Render_SB01_INV_001_preserves_template_executor_kind_options`; `NormalizeForSelection_SB01_INV_002_accepts_current_template_executor_vocabulary`; `Process_template_vocabulary_SB01_INV_001_maps_to_supported_ui_and_domain_options`
- Changed source files: `repo://src/CanDoItAll.Modules.Processes/Definitions/ProcessDefinitionEnums.cs`; `repo://src/CanDoItAll.Modules.Processes/Components/ProcessRoleEditorForm.razor`; `repo://tests/CanDoItAll.Tests.Components/ProcessRoleEditorFormTests.cs`
- Production assertions: `ProcessRoleExecutorKindOptions` is the typed option catalog and `ProcessRoleEditorForm` binds selection through it.
- Red-team negative case: A template role using `person-or-agent` must stay selectable and must not normalize to `AI agent`.
- Downstream dependency check: Template projection and role editor tests both assert the option vocabulary so future template drift fails before reload.

## SB01-INV-003

- Invariant ID: `SB01-INV-003`
- Source raw note: `N001`
- Expected behavior: Responsibility options include `Accountable` as a first-class process role assignment value with canvas/runtime support.
- Disallowed shallow implementation: Adding only a UI label while runtime priority, canvas ports, or enum parsing still reject `Accountable`.
- Failing-first test: `bundle://proof/SB01/transcripts/failing-first.txt`
- Passing test: `Render_SB01_INV_003_exposes_accountable_responsibility_option`; `Dotnet_feature_template_SB01_INV_002_preserves_accountable_decision_record_and_approval_required`
- Changed source files: `repo://src/CanDoItAll.Modules.Processes/Definitions/ProcessDefinitionEnums.cs`; `repo://src/CanDoItAll.Modules.Processes/Canvas/ProcessCanvasCatalog.cs`; `repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessesService.Runtime.RunStart.cs`
- Production assertions: `Accountable` is parsed as a domain enum value and participates in runtime assignment priority.
- Red-team negative case: A template step with `ResponsibilityKind: Accountable` cannot silently fall back to `Responsible`.
- Downstream dependency check: Runtime read models, dispatcher ordering, and canvas categories were updated with accountable-specific handling.

## SB01-INV-004

- Invariant ID: `SB01-INV-004`
- Source raw note: `N001`
- Expected behavior: Artifact options include `DecisionRecord` and trust options include `ApprovalRequired`; runtime validation treats approval-required artifacts as approved-only.
- Disallowed shallow implementation: Adding enum names while completion, trust, linter, or dispatch logic still treats the new values as `Other` or unsatisfied.
- Failing-first test: `bundle://proof/SB01/transcripts/failing-first.txt`
- Passing test: `Render_SB01_INV_004_exposes_decision_record_and_approval_required_options`; `Dotnet_feature_template_SB01_INV_002_preserves_accountable_decision_record_and_approval_required`
- Changed source files: `repo://src/CanDoItAll.Modules.Processes/Definitions/ProcessDefinitionEnums.cs`; `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ArtifactValidation.cs`; `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/WorkflowSubprocessArtifactMapper.cs`
- Production assertions: `DecisionRecord` is handled as decision-like where decisions are required, and `ApprovalRequired` is satisfied only by approved artifacts.
- Red-team negative case: Review-required evidence cannot satisfy an approval-required artifact.
- Downstream dependency check: Completion finalization, tool validation, metadata, linter, dispatch, and invariant auditor paths were updated.

## Production Behavior Artifact Matrix

| Artifact | Producer | Consumer | Lifecycle | Negative proof |
| --- | --- | --- | --- | --- |
| `ProcessRoleExecutorKindOptions` | `repo://src/CanDoItAll.Modules.Processes/Definitions/ProcessDefinitionEnums.cs` | `repo://src/CanDoItAll.Modules.Processes/Components/ProcessRoleEditorForm.razor` | Template executor vocabulary is normalized into strongly typed UI selection values and persisted back to the role model; covered by `bundle://proof/SB01/transcripts/passing-tests.txt`. | `person-or-agent` stays selectable and is not narrowed to agent-only behavior; covered by `NormalizeForSelection_SB01_INV_002_accepts_current_template_executor_vocabulary`. |
| `Accountable` responsibility | `repo://src/CanDoItAll.Modules.Processes/Definitions/ProcessDefinitionEnums.cs` | `repo://src/CanDoItAll.Modules.Processes/Canvas/ProcessCanvasCatalog.cs`; `repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessesService.Runtime.RunStart.cs` | Template parsing, role assignment priority, canvas categories, and read-model ports carry the accountable role as a first-class value; covered by `bundle://proof/SB01/transcripts/passing-tests.txt`. | `ResponsibilityKind: Accountable` cannot silently fall back to `Responsible`; covered by `Dotnet_feature_template_SB01_INV_002_preserves_accountable_decision_record_and_approval_required`. |
| `DecisionRecord` and `ApprovalRequired` artifacts | `repo://src/CanDoItAll.Modules.Processes/Definitions/ProcessDefinitionEnums.cs` | `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ArtifactValidation.cs`; `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/WorkflowSubprocessArtifactMapper.cs` | Template parsing, artifact validation, finalization, linter, dispatch, and subprocess mapping use decision-like and approved-only semantics; covered by `bundle://proof/SB01/transcripts/passing-tests.txt`. | Review-required evidence cannot satisfy an approval-required artifact; covered by `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ArtifactValidation.cs`. |
