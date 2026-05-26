# SB01 Proof Manifest

## Scope

- Subbundle: `SB01`
- Status: `Completed`
- Semantic invariant contract: `bundle://proof/SB01/semantic-invariants.md`

## Evidence

- Failing-first transcript: `bundle://proof/SB01/transcripts/failing-first.txt`
- Passing transcript: `bundle://proof/SB01/transcripts/passing-tests.txt`
- Anti-stub audit transcript: `bundle://proof/SB01/transcripts/anti-stub-audit.txt`
- Changed-file hashes transcript: `bundle://proof/SB01/transcripts/changed-file-hashes.txt`

## Test Proof

- Test name: `Render_SB01_INV_001_preserves_template_executor_kind_options`
- Test name: `NormalizeForSelection_SB01_INV_002_accepts_current_template_executor_vocabulary`
- Test name: `Render_SB01_INV_003_exposes_accountable_responsibility_option`
- Test name: `Render_SB01_INV_004_exposes_decision_record_and_approval_required_options`
- Test name: `Process_template_vocabulary_SB01_INV_001_maps_to_supported_ui_and_domain_options`
- Test name: `Dotnet_feature_template_SB01_INV_002_preserves_accountable_decision_record_and_approval_required`

## Production Behavior Artifact Matrix

| Artifact | Producer | Consumer | Lifecycle | Negative proof |
| --- | --- | --- | --- | --- |
| `ProcessRoleExecutorKindOptions` | `repo://src/CanDoItAll.Modules.Processes/Definitions/ProcessDefinitionEnums.cs` | `repo://src/CanDoItAll.Modules.Processes/Components/ProcessRoleEditorForm.razor` | Template executor vocabulary is normalized into strongly typed UI selection values and persisted back to the role model; covered by `bundle://proof/SB01/transcripts/passing-tests.txt`. | `person-or-agent` stays selectable and is not narrowed to agent-only behavior; covered by `NormalizeForSelection_SB01_INV_002_accepts_current_template_executor_vocabulary`. |
| `Accountable` responsibility | `repo://src/CanDoItAll.Modules.Processes/Definitions/ProcessDefinitionEnums.cs` | `repo://src/CanDoItAll.Modules.Processes/Canvas/ProcessCanvasCatalog.cs`; `repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessesService.Runtime.RunStart.cs` | Template parsing, role assignment priority, canvas categories, and read-model ports carry the accountable role as a first-class value; covered by `bundle://proof/SB01/transcripts/passing-tests.txt`. | `ResponsibilityKind: Accountable` cannot silently fall back to `Responsible`; covered by `Dotnet_feature_template_SB01_INV_002_preserves_accountable_decision_record_and_approval_required`. |
| `DecisionRecord` and `ApprovalRequired` artifacts | `repo://src/CanDoItAll.Modules.Processes/Definitions/ProcessDefinitionEnums.cs` | `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ArtifactValidation.cs`; `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/WorkflowSubprocessArtifactMapper.cs` | Template parsing, artifact validation, finalization, linter, dispatch, and subprocess mapping use decision-like and approved-only semantics; covered by `bundle://proof/SB01/transcripts/passing-tests.txt`. | Review-required evidence cannot satisfy an approval-required artifact; covered by `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ArtifactValidation.cs`. |

## Changed File Hashes

- `repo://src/CanDoItAll.Modules.Processes/Definitions/ProcessDefinitionEnums.cs` SHA-256 `1de16a743d7884c5076fb986fcd045e3ea4199b3188a9a32aeb975ad8d8cee0e`
- `repo://src/CanDoItAll.Modules.Processes/Components/ProcessRoleEditorForm.razor` SHA-256 `25ae85ba70f98d86232a7a23c96e2ded8bdee87028e07ee2beb8faab0ec4721f`
- `repo://src/CanDoItAll.Modules.Processes/Canvas/ProcessCanvasCatalog.cs` SHA-256 `e490cb5a53e227a28e6bc6233d8e69e4b5efddbac3b34a0e91e7312227e10954`
- `repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessesService.Runtime.RunStart.cs` SHA-256 `8a508d293c9a9ea3b9085d37abdd23532e8f9c5c919bbd1efc618431be5ee731`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/WorkflowSubprocessArtifactMapper.cs` SHA-256 `8626ad6b6c5e1c0415ab0a6fef3ab5eb4f5b134cc7702d864d3d2d8da2ace1b3`
- `repo://tests/CanDoItAll.Tests.Components/ProcessRoleEditorFormTests.cs` SHA-256 `343f350311fe70acefa8ecf4e250d6436e206d2d8d575707e2898c333df22758`
- `repo://tests/CanDoItAll.Tests.Components/ProcessStepRoleAssignmentEditorTests.cs` SHA-256 `b472ca4c4fda381b3b4241ccc04a3fe43fd7e2a1e12a99f9c457fb1d7ca774f3`
- `repo://tests/CanDoItAll.Tests.Components/ProcessArtifactExpectationEditorTests.cs` SHA-256 `9f2dffe541a41d68b1a75fca17fef7d7350afe9929a9b5b22172eeb4c23a7093`
- `repo://tests/CanDoItAll.Tests.Integration/ProcessTemplateGovernanceTests.cs` SHA-256 `621f52f9b0f4a9cc21c308a4b1c8ddf5d557d1fab76ca437f97bfa2b2f70032c`

## Closure

- Raw note owned: `N001`
- Shipped behavior: UI/domain/template vocabulary parity is implemented for executor, responsibility, artifact kind, and artifact trust options.
- Source proof: `repo://src/CanDoItAll.Modules.Processes/Definitions/ProcessDefinitionEnums.cs`; `repo://src/CanDoItAll.Modules.Processes/Components/ProcessRoleEditorForm.razor`
- Test proof: `bundle://proof/SB01/transcripts/passing-tests.txt`
- Shallow-pass trap: tests assert model mutation and template projection, not only visible text.
- Adversarial negative proof: `bundle://proof/SB01/transcripts/failing-first.txt`
- Semantic positive proof: `bundle://proof/SB01/semantic-invariants.md`
- Anti-stub audit: `bundle://proof/SB01/transcripts/anti-stub-audit.txt`
