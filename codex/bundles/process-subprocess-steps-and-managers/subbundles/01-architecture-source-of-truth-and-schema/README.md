# architecture source of truth and schema

## Status

- `Completed`

## Objective

- Add the strongly typed definition/runtime schema needed for subprocess steps and process manager overrides without duplicating runtime truth.

## Covered Inputs

- Process can use another process as a process step.
- Subprocess state must remain observable by the parent process.
- Prevent split source of truth for large process trees.
- Add per-process manager override metadata.
- Analyze Agent Framework 1.3 before implementation.

## Prerequisites

- none

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Definitions\ProcessDefinitionEnums.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Definitions\ProcessDefinitionEditorModels.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Persistence\Entities\ProcessDefinitionEntities.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Persistence\Entities\ProcessRuntimeModels.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Persistence\Configurations\ProcessDefinitionEntityConfigurations.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Persistence\Configurations\ProcessRuntimeEntityConfigurations.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ImportExport\ProcessImportExportModels.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Templates\ProcessTemplatePackModels.cs`
- `C:\repositories\agent-framework\dotnet\samples\03-workflows\_StartHere\05_SubWorkflows\Program.cs`

## Deliverables

- Add `ProcessStepKind.Subprocess`.
- Add subprocess definition reference fields to editor, persistence, import/export, and template models.
- Add manager override fields to process version/editor/run snapshots.
- Add runtime hierarchy fields to `ProcessRun`.
- Add EF configuration/indexes and migrations.

## Dependency Impact

- Runtime orchestration, canvas projections, manager reporting, and templates all depend on these contracts. Weak proof here invalidates the remaining bundle.

## Validation Depth

- `Critical foundation`

## Implementation Steps

1. Add enum/model fields and keep names strongly typed.
2. Update EF configurations and migrations for SQL providers.
3. Update definition save/read/import/export/template mapping.
4. Add or update targeted model/persistence tests.
5. Rebuild affected projects.

## Scope Exceptions

- Do not implement child run dispatch in this subbundle.
- Do not implement UI actions in this subbundle.

## Do Not Do

- Do not persist AgentFramework SDK workflow objects in process tables.
- Do not create a duplicate child status column on the parent step as canonical truth.
- Do not use string-only process or agent identifiers.

## Acceptance Checklist

- Subprocess step kind round-trips through definitions.
- Subprocess target id and snapshot name round-trip through persistence.
- Process run hierarchy fields can be saved and queried.
- Manager override id and snapshot name round-trip through version/run models.
- Migrations are present and build.

## Proof Required

- `dotnet build C:\repositories\CanDoItAll\CanDoItAll.slnx --no-restore`
- Targeted tests for process definition persistence/import/export if available.
- Execution report update with commands and outcomes.

## Browser Validation Logging

- `N/A`

## Progression Gate

- Continue only when schema builds and no source-of-truth duplicate is introduced.

## Suggested Agent Prompt

```text
Implement only the subprocess and manager source-of-truth schema. Keep ProcessRun as the canonical runtime hierarchy owner and do not implement UI/runtime dispatch yet.
```
