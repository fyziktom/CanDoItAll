# Source Artifacts

## Raw Request

- `bundle://inputs/00-original-request.md`

## Source Inventory

- `repo://src/CanDoItAll.Modules.Processes/Definitions/ProcessDefinitionEnums.cs`
- `repo://src/CanDoItAll.Modules.Processes/Definitions/ProcessDefinitionEditorModels.cs`
- `repo://src/CanDoItAll.Modules.Processes/Components/ProcessRoleEditorForm.razor`
- `repo://src/CanDoItAll.Modules.Processes/Components/ProcessStepEditorForm.razor`
- `repo://src/CanDoItAll.Modules.Processes/Components/ProcessStepRoleAssignmentEditor.razor`
- `repo://src/CanDoItAll.Modules.Processes/Components/ProcessArtifactExpectationEditor.razor`
- `repo://Templates/Processes/toolbox/role-templates.json`
- `repo://Templates/Processes/toolbox/step-templates.json`
- `repo://Templates/Processes/processes/**/definition.json`
- `repo://src/CanDoItAll.Modules.Processes/Services/ProcessCatalogWarmupService.cs`
- `repo://src/CanDoItAll.Web/appsettings.Development.json`
- `repo://src/CanDoItAll.Modules.Processes/Persistence/Configurations/ProcessDefinitionEntityConfigurations.cs`

## Initial Observations

- The UI already uses shared `SurfaceCard`, `PanelCard`, `Stack`, `Grid`, `FormField`, `Cluster`, and `InputSelect` patterns, so implementation should extend existing controls rather than introduce page-local layout wrappers.
- The process template pack uses role executor vocabulary `person`, `agent`, `person-or-agent`, and `AI agent`.
- The process template pack uses step/role/artifact vocabulary that includes `Accountable`, `DecisionRecord`, and `ApprovalRequired`; these are not currently defined as domain enum values.
- Process definition enum properties are persisted with `HasConversion<string>()`, so adding enum values is safer than if the database stored numeric ordinals.
- The development database connection in `src/CanDoItAll.Web/appsettings.Development.json` targets PostgreSQL database `candoitall_development`.
