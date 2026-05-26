# Reviewed source observations

Reviewed through the GitHub connector on branch `processes-hardening`, head `phase6`.

Key source paths reviewed:
- `src/CanDoItAll.Modules.Processes/Definitions/ProcessDefinitionEnums.cs`
- `src/CanDoItAll.Modules.Processes/Persistence/Entities/ProcessDefinitionEntities.cs`
- `src/CanDoItAll.Modules.Processes/Persistence/Entities/ProcessRuntimeModels.cs`
- `src/CanDoItAll.Modules.Processes/Persistence/Configurations/ProcessDefinitionEntityConfigurations.cs`
- `src/CanDoItAll.Modules.Processes/Persistence/Configurations/ProcessRuntimeEntityConfigurations.cs`
- `src/CanDoItAll.Modules.Processes/Definitions/ProcessDefinitionEditorModels.cs`
- `src/CanDoItAll.Modules.Processes/Definitions/ProcessStepOperationContractState.cs`
- `src/CanDoItAll.Modules.Processes/Definitions/ProcessDefinitionLinter.cs`
- `src/CanDoItAll.Modules.Processes/Services/ProcessesService.cs`
- `src/CanDoItAll.Modules.Processes/Services/ProcessesService.Publication.cs`
- `src/CanDoItAll.Modules.Processes/Services/ProcessesService.Support.cs`
- `src/CanDoItAll.Modules.Processes/Runtime/ProcessesService.Runtime.RunStart.cs`
- `src/CanDoItAll.Modules.Processes/Runtime/ProcessesService.Runtime.StepTransitions.cs`
- `src/CanDoItAll.Modules.Processes/Runtime/ProcessesService.Runtime.Operations.cs`
- `src/CanDoItAll.Modules.Processes/Runtime/ProcessStepRunBlockState.cs`
- `src/CanDoItAll.Modules.Processes/Runtime/ProcessArtifactProjectionLineage.cs`
- `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ExecutionMetadata.cs`
- `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.StepCompletionFinalizer.cs`
- `src/CanDoItAll.AgentFramework.Core/Execution/ExecutionInvocationMetadata.cs`
- `src/CanDoItAll.AgentFramework.Core/ToolPolicy/AgentToolInvocationPolicy.cs`
- `src/CanDoItAll.AgentFramework.Core/Workspace/Audit/WorkspaceExecutionAuditContext.cs`
- `src/CanDoItAll.AgentFramework.Maf/Runtime/MafAgentRuntime.AgentFactory.cs`
- `src/CanDoItAll.Modules.Processes/Components/ProcessStepEditorForm.razor`

Important positive changes:
- Typed operation enums are now public process concepts.
- Operation contract fields are persisted on process step definitions.
- Editor model and UI expose operation contract fields.
- Dispatch metadata emits allowed operations, target scope, product mutation flag, and grounding ledger.
- Tool policy enforces several operation requirements.
- Artifact projection lineage and projection identity hash fields exist.
- Step block reason code and recovery options exist.
- Publish and run-start resolve effective lint mode from contract mode/criticality/autonomy.
- Blazor templates were updated with operation contracts and artifact recovery text.

Potentially incomplete:
- Processes API/tool request and response schemas may not be fully synchronized.
- Related skills/docs were not visibly updated in phase6 file changes beyond the bundle itself and templates.
- Workflow/subprocess output mappings exist on `ProcessArtifactExpectation`, but linter/runtime enforcement still appears partial.
- `TransitionStepAsync` still has a lighter required-artifact gate than the finalizer.
- Block cause classification still infers from reason text in important paths.
