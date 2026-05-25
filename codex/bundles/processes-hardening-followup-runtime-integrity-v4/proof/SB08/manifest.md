# SB08 Proof Manifest

## Status

Completed.

## Source Assertions

- `repo://src/CanDoItAll.Modules.Processes/Definitions/ProcessDefinitionEnums.cs` defines public typed `ProcessStepOperation` and `ProcessStepTargetScope` values used by persistence, editor models, import/export, templates, linter, and dispatch metadata.
- `repo://src/CanDoItAll.Modules.Processes/Persistence/Entities/ProcessDefinitionEntities.cs:169` adds durable step fields for `AllowedOperations` and `OperationTargetScope`.
- `repo://src/CanDoItAll.Modules.Processes/Persistence/Configurations/ProcessDefinitionEntityConfigurations.cs:163` persists allowed operations as normalized JSON and target scope as an enum string column.
- `repo://src/CanDoItAll.Migrations.PostgreSql/Migrations/20260525153000_ProcessStepOperationContract.cs:16` adds the PostgreSQL columns and `repo://src/CanDoItAll.Migrations.PostgreSql/Migrations/AppDbContextModelSnapshot.cs:12653` records them in the model snapshot.
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ExecutionMetadata.cs:317` resolves persisted operation contracts before text parsing and `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ExecutionMetadata.cs:407` maps typed fields into runtime invocation metadata.
- `repo://src/CanDoItAll.Modules.Processes/Persistence/ProcessesService.Persistence.DefinitionChildren.Steps.cs:41`, `repo://src/CanDoItAll.Modules.Processes/Services/ProcessesService.cs:171`, and `repo://src/CanDoItAll.Modules.Processes/ImportExport/ProcessImportExportModels.cs:84` cover save/load/import/export lifecycle.
- `repo://src/CanDoItAll.Modules.Processes/Components/ProcessStepEditorForm.razor:148` exposes the target-scope selector and `repo://tests/CanDoItAll.Tests.Components/ProcessStepEditorFormTests.cs:101` covers model updates.
- `repo://src/CanDoItAll.Modules.Processes/Definitions/ProcessDefinitionLinter.cs:136` warns when product mutation boundaries are inferred from text and `repo://src/CanDoItAll.Modules.Processes/Definitions/ProcessDefinitionLinter.cs:325` rejects partial typed contracts.

## Production Behavior Artifact Matrix

| Artifact/signal | Producer | Consumer | Lifecycle | Negative proof |
| --- | --- | --- | --- | --- |
| Step operation contract columns | Process definition editor save path and PostgreSQL migration | EF entity materialization, dispatch metadata builder, import/export, template projection | Durable on `Processes_StepDefinitions`; cloned into drafts and preserved through publish/import/export | SB08 lifecycle test proves values survive save, export, import, publish, and next-draft clone |
| Runtime operation metadata | `TryResolvePersistedOperationContract` from typed step fields | execution prompt/metadata and downstream tool-policy boundary calculations | Recomputed for each dispatch candidate from persisted definition state | SB08 metadata test proves typed external artifact destination beats missing text markers |
| Editor controls | `ProcessStepEditorForm` target-scope select and operation checkboxes | editor model save path and browser/component flows | User-editable model state, then durable after save | bUnit and Playwright tests prove selector/checkbox update state without Blazor circuit failure |
| Linter inferred-contract issue | `ProcessDefinitionLinter.AddBoundaryIssues` | definition analysis / publish readiness consumers | Produced during definition lint evaluation | SB08 linter tests prove text-inferred contracts warn and partial typed contracts fail strictness |

## Failing-First Or Red-Team Proof

Transcript: `bundle://proof/SB08/transcripts/failing-first.txt`

## Passing Proof

Transcript: `bundle://proof/SB08/transcripts/passing.txt`

## Anti-Stub Audit

Transcript: `bundle://proof/SB08/transcripts/anti-stub-audit.txt`

## Changed-File Hashes

Transcript: `bundle://proof/SB08/transcripts/changed-file-hashes.txt`

## Validation

Passed:

- `dotnet test tests/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj --filter "FullyQualifiedName~SB08_INV_001" --no-restore -v minimal`
- `dotnet test tests/CanDoItAll.Tests.Components/CanDoItAll.Tests.Components.csproj --filter "FullyQualifiedName~Render_SB08_INV_001" --no-restore -v minimal`
- `dotnet test tests/CanDoItAll.Tests.Playwright/CanDoItAll.Tests.Playwright.csproj --filter "FullyQualifiedName~Process_step_operation_contract_editor_controls_work_in_browser" --no-restore -v minimal`

Known unrelated warning noise: MSB3277 reports existing EntityFrameworkCore.Relational 10.0.0/10.0.4 conflicts during build.

## Blockers

None.
