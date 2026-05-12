# Scope Inventory

| Area | Path | Role |
| --- | --- | --- |
| Current compiled seed service | `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Catalog\WorkflowExampleCatalogSeedService.cs` | Remove compiled default workflow graphs and seed from loaded templates. |
| Existing process template precedent | `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Templates\ProcessTemplatePackLoader.cs` | Reuse root-resolution and pack-loader design ideas. |
| Existing process templates | `C:\repositories\CanDoItAll\Templates\Processes\manifest.json` | Local model for file-backed template layout. |
| Workflow domain models | `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Models\Workflows\WorkflowModels.cs` | Target strongly typed graph models loaded from YAML. |
| Workflow catalog contracts | `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Core\Workflows\WorkflowCatalogContracts.cs` | Seeding destination contracts. |
| Workflow validator | `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Core\Workflows\WorkflowDefinitionValidator.cs` | Required template validation gate. |
| Unit tests | `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Unit\WorkflowCatalogTests.cs` | Add loader and seeding regression coverage. |
| MAF YAML reference | `C:\repositories\agent-framework\dotnet\src\Microsoft.Agents.AI.Workflows.Declarative\DeclarativeWorkflowBuilder.cs` | Reference for file-backed YAML load behavior. |
