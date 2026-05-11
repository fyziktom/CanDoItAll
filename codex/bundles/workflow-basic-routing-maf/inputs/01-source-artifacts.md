# Source Artifacts

## Uploaded Repository

- Uploaded ZIP: `/mnt/data/CanDoItAll-agents-integration (4).zip`
- Extracted repo inspected at: `/mnt/data/cando/CanDoItAll-agents-integration`

## Relevant External References Reviewed

- Microsoft Agent Framework workflow samples: `dotnet/samples/03-workflows`.
- Conditional routing samples: `dotnet/samples/03-workflows/ConditionalEdges/01_EdgeCondition`, `02_SwitchCase`, and `03_MultiSelection`.
- MAF API references for `WorkflowBuilder.AddEdge`, `WorkflowBuilderExtensions.AddSwitch`, and `WorkflowBuilder.AddFanOutEdge`.

## Local Current-State Highlights

- MAF package reference: `Microsoft.Agents.AI.Workflows` version `1.3.0` in `src/CanDoItAll.AgentFramework.Maf/CanDoItAll.AgentFramework.Maf.csproj`.
- Workflow domain model: `src/CanDoItAll.AgentFramework.Models/Workflows/WorkflowModels.cs`.
- MAF compiler: `src/CanDoItAll.AgentFramework.Maf/Runtime/Workflows/MafWorkflowCompiler.cs`.
- Canvas models and editor: `src/CanDoItAll.Modules.AgentFramework/Pages/Components/WorkflowCanvasModels.cs` and `WorkflowCanvasEditor.*`.
