# Current State

## UI State

- `repo://src/CanDoItAll.Modules.Processes/Components/ProcessRoleEditorForm.razor` renders a preferred executor dropdown with only `Human`, `AI agent`, and `Workflow`.
- `ProcessExecutorKindNames.Normalize("person-or-agent")` currently resolves through the generic `agent` substring path to `AI agent`, which narrows the template contract.
- `repo://src/CanDoItAll.Modules.Processes/Components/ProcessStepEditorForm.razor` renders step kind, target scope, and allowed operations from domain enums.
- `repo://src/CanDoItAll.Modules.Processes/Components/ProcessStepRoleAssignmentEditor.razor` renders responsibility kinds from `ProcessResponsibilityKind`.
- `repo://src/CanDoItAll.Modules.Processes/Components/ProcessArtifactExpectationEditor.razor` renders artifact kind, trust requirement, and sensitivity from enums.

## Template Vocabulary State

Observed non-empty template values under `repo://Templates/Processes`:

- `PreferredExecutorKind`: `person`, `agent`, `person-or-agent`, `AI agent`.
- `ResponsibilityKind`: `Responsible`, `Reviewer`, `Approver`, `Backup`, `Accountable`, plus numeric enum values in projected sidecars.
- `ArtifactKind`: `Brief`, `Evidence`, `Decision`, `DecisionRecord`, `Deliverable`, `Transcript`, `Checklist`, `Prompt`, `Dataset`, plus numeric sidecar values.
- `TrustRequirement`: `ReviewRequired`, `HumanApproved`, `ApprovalRequired`, plus numeric sidecar values.
- `StepKind`, `AllowedOperations`, `OperationTargetScope`, `SensitivityLevel`, and `PreferredProjectAssignmentRole` are otherwise represented by current typed values or numeric sidecar values.

## Persistence State

- Process definition enum properties are stored as strings in `repo://src/CanDoItAll.Modules.Processes/Persistence/Configurations/ProcessDefinitionEntityConfigurations.cs`.
- Adding enum values does not require rewriting existing rows because persisted values are textual.

## Development Database State

- Development configuration uses PostgreSQL database `candoitall_development` from `repo://src/CanDoItAll.Web/appsettings.Development.json`.
- Process-owned tables use the `Processes_` prefix.
- Existing app warmup can import and publish default process templates through `ProcessCatalogWarmupService.WarmupAsync(synchronizeExistingDefinitions: true)`.
