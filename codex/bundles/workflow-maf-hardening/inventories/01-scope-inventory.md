# Scope Inventory And Audit Checklist

Codex must complete this inventory in SB01 before architecture edits.

## Mandatory source scan

Run from repository root:

```powershell
rg -n "Workflow|WorkflowBuilder|Executor|MessageHandler|IWorkflowContext|InProcessExecution|DurableTask|ApprovalRequiredAIFunction|FunctionApprovalRequestContent|ExecutorId|Plugin" src tests Templates .codex docs
```

## Areas to classify

### Domain model and persistence

- `src/CanDoItAll.AgentFramework.Models`
- `src/CanDoItAll.AgentFramework.Core`
- `src/CanDoItAll.AgentFramework.Persistence`
- Any EF entities/migrations for workflows, runs, artifacts, plugin settings, or approvals.

### MAF integration

- `src/CanDoItAll.AgentFramework.Maf`
- All references to `Microsoft.Agents.AI.*`
- All native MAF workflow/executor/event abstractions.

### Agents module and UI

- `src/CanDoItAll.Modules.AgentFramework`
- Workflow pages/components/dialogs/view models.
- Catalog, component library, seed services, preview simulation, runtime launch code.

### Template pack

- `Templates/Workflows/manifest.yaml`
- `Templates/Workflows/workflows/*.yaml`
- `WorkflowTemplatePackLoader`
- `WorkflowExampleCatalogSeedService`
- Tests covering template loading and seeded examples.

### Plugin module and plugin projects

- `src/CanDoItAll.Modules.Plugins`
- `src/CanDoItAll.Plugins.Abstractions`
- `src/plugins/CanDoItAll.Plugin.Email`
- `src/plugins/CanDoItAll.Plugin.Gmail`
- `src/plugins/CanDoItAll.Plugin.Office365`
- `src/plugins/CanDoItAll.Plugin.Docker`
- Any plugin registration, descriptor, command, executor, capability, credential, or OAuth code.

### Runtime and evidence

- Workflow run services.
- Event/artifact services.
- Telemetry/OpenTelemetry integration.
- Human approval and external request handling.
- Project structure/workbench workflow launch surfaces.

## Inventory output required

Create or update:

- `.codex/bundles/workflow-maf-hardening/inventories/02-local-source-inventory.md`
- `.codex/bundles/workflow-maf-hardening/inventories/03-maf-version-baseline.md`
- `.codex/bundles/workflow-maf-hardening/inventories/04-plugin-executor-inventory.md`

Each entry must include:

- Path
- Responsibility
- Current MAF usage level: `none`, `model-only`, `adapter`, `native-executor`, `runtime`, `test`
- Risk: `low`, `medium`, `high`, `critical`
- Suggested subbundle owner
