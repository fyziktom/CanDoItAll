# Source Artifacts

## Skill Inputs

| Artifact | Durable reference | Use |
| --- | --- | --- |
| Bundle workflow skill | `C:/Users/lucys/.codex/skills/candoitall-bundle-workflow/SKILL.md` | Chose preparation path and readiness gate contract. |
| Bundle preparation skill | `C:/Users/lucys/.codex/skills/candoitall-bundle-preparation/SKILL.md` | Defined initiative bundle structure and subbundle contract. |
| Bundle validator skill | `C:/Users/lucys/.codex/skills/candoitall-bundle-validator/SKILL.md` | Defined prepared-stage readiness requirements. |
| Subbundle validator skill | `C:/Users/lucys/.codex/skills/candoitall-subbundle-validator/SKILL.md` | Defined entry and closure gate requirements. |
| Performance skills | `C:/Users/lucys/.codex/skills/analyzing-dotnet-performance/SKILL.md`, `C:/Users/lucys/.codex/skills/optimizing-dotnet-performance/SKILL.md` | Required performance review and scan rules. |
| CodeAnalytics skill | `C:/Users/lucys/.codex/skills/candoitall-codeanalytics-mcp/SKILL.md` | Used for scoped solution/project inventory. |
| MSBuild and directory build skills | `C:/Users/lucys/.codex/skills/msbuild/SKILL.md`, `C:/Users/lucys/.codex/skills/directory-build-organization/SKILL.md` | Used for new project/reference sequencing concerns. |
| Spreadsheet skill | `C:/Users/lucys/.codex/plugins/cache/openai-primary-runtime/spreadsheets/26.623.12021/skills/spreadsheets/SKILL.md` | Used to create the required XLSX mapping. |

## Repository Evidence

| Surface | Durable reference | Current observation |
| --- | --- | --- |
| Current solution graph | `repo://CanDoItAll.slnx` | Existing capability isolation projects exist for tools, skills, MCPs, processes, and plugins. No dedicated workflow projects exist yet. |
| Prior isolation precedent | `repo://codex/bundles/skill-tool-mcp-isolation-template-migration/README.md` | Previous long-run bundle used contracts, implementation projects, templates, MAF reconnection, and hardening checkpoints. |
| Workflow models | `repo://src/CanDoItAll.AgentFramework.Models/Workflows/WorkflowModels.cs`, `repo://src/CanDoItAll.AgentFramework.Models/Workflows/WorkflowExecutorModels.cs` | Typed workflow ids, definitions, executor descriptors, policies, side effects, and settings already live in Models. |
| Core workflow services | `repo://src/CanDoItAll.AgentFramework.Core/Workflows` | Contracts, validator, catalog services, runtime manager, executor catalog/invoker, observability, routing, stores, and payload policy are mixed in Core. |
| MAF workflow executors | `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/Workflows` | Default executor implementations, descriptor builder, MAF compiler, in-process backend, LLM component invoker, event normalizer, and workflow helper code are in MAF. |
| Host registration | `repo://src/CanDoItAll.AgentFramework.Hosting/AgentFrameworkServiceCollectionExtensions.cs:67` | `AddAgentFrameworkCore` registers built-in executors, catalog, validator, runtime manager, stores, MAF compiler, backend, bridge, and test runner in one method. |
| Built-in executor DI | `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/Workflows/BuiltInWorkflowExecutorServiceCollectionExtensions.cs:9` | Registers descriptor source and default executors directly from MAF. |
| Built-in descriptor factory | `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/Workflows/BuiltInWorkflowExecutorDescriptors.cs:16` | MAF owns descriptor creation and settings schema reflection for built-in executors. |
| Plugin descriptor projection | `repo://src/CanDoItAll.Modules.Plugins/Catalog/PluginWorkflowExecutorDescriptorSource.cs:7` | Plugin descriptors are converted to workflow executor descriptors with grants, source metadata, side effects, and deterministic test-mode metadata. |
| Plugin runtime package registration | `repo://src/CanDoItAll.Modules.Plugins/Catalog/PluginPackageServices.cs:1041` | Runtime plugin packages register types assignable to `IWorkflowExecutor` and wrap source metadata. |
| Plugin abstractions | `repo://src/CanDoItAll.Plugins.Abstractions/PluginExecutionContracts.cs:17`, `repo://src/CanDoItAll.Plugins.Abstractions/PluginManifestContracts.cs:68` | Plugin execution and manifest contracts already expose workflow executor concepts, but reference current workflow model/core types. |
| Bundled plugin executors | `repo://src/plugins/CanDoItAll.Plugin.Docker/DockerWorkflowExecutors.cs`, `repo://src/plugins/CanDoItAll.Plugin.Gmail/GmailWorkflowExecutor.cs`, `repo://src/plugins/CanDoItAll.Plugin.Office365/Office365WorkflowExecutor.cs` | Docker, Gmail, and Office365 provide production executors with grants, secrets/OAuth, host command, network, side-effect, and simulation behavior. |
| Workflow templates | `repo://Templates/Workflows/manifest.yaml`, `repo://Templates/Workflows/workflows/*.yaml` | File-driven workflow templates already exist and reference executor ids. |
| Workflow template loader | `repo://src/CanDoItAll.Modules.AgentFramework/Catalog/WorkflowTemplatePackLoader.cs` | Template loading and YAML DTOs currently live in the Blazor module and should move to workflow template project. |
| Workflow UI | `repo://src/CanDoItAll.Modules.AgentFramework/Pages/WorkflowsPage.razor`, `repo://src/CanDoItAll.Modules.AgentFramework/Pages/Components/WorkflowCanvasEditor.razor.cs` | UI consumes workflow catalog, executor catalog, backend catalog, and template-derived definitions. |
| Workbench workflow nodes | `repo://src/CanDoItAll.Modules.Workbench/ProjectStructure/ProjectStructureWorkflowNodeService.cs`, `repo://src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.WorkflowNodes.cs` | Project structure creates, starts, previews, and displays workflow nodes. |
| Workflow API | `repo://src/CanDoItAll.Web/Api/WorkflowsApi.cs` | API is a compatibility surface for definitions, runs, executor catalog, tests, imports, exports, and settings. |
| Existing tests | `repo://tests/CanDoItAll.Tests.Unit/WorkflowFoundationTests.cs`, `repo://tests/CanDoItAll.Tests.Unit/WorkflowExecutorTests.cs`, `repo://tests/CanDoItAll.Tests.Integration/WorkflowApiIntegrationTests.cs`, `repo://tests/CanDoItAll.Tests.Playwright/WorkflowShellSmokeTests.cs` | Test coverage exists but will need project-boundary and regression expansion. |

## CodeAnalytics Evidence

- Initial preparation snapshot id: `snap-20260629133443-f4ee046c`
- Re-audit snapshot id: `snap-20260629143729-e43d210b`
- Scope: `CanDoItAll.AgentFramework.Maf`, `Core`, `Persistence`, `Modules.AgentFramework`, `Modules.Workbench`, `Modules.Plugins`, plugins, processes projects, Web, unit and integration tests.
- Re-audit snapshot result: 20 source projects, 587 source documents, no blocking snapshot errors.
- Project graph finding: process work already has `Contracts`, `Abstractions`, `Core`, `Builder`, `Runtime`, `Templates`, `Drivers.Abstractions`, and `Drivers.Standard`; workflows do not have equivalent dedicated projects.

## Generated Bundle Artifact

| Artifact | Durable reference | Purpose |
| --- | --- | --- |
| Mapping workbook | `bundle://inventories/workflow-node-project-isolation-map.xlsx` | XLSX mapping of rework surfaces, proposed projects, owning subbundles, risks, and validation. |
