# Current State

## What Is Already Sound

- `IWorkflowExecutor`, `IWorkflowExecutorCatalog`, `IWorkflowExecutorInvoker`, descriptor schemas, approval, timeout, retry, audit, and redaction already form a useful executor boundary.
- Workspace-file and spreadsheet runtime tools and executors already demonstrate the right reuse direction: both adapt shared services instead of invoking each other.
- Workflow LLM execution already records a `WorkflowUsageMetrics` payload in workflow events.
- Scheduler and project-structure launches already call the workflow runtime manager with context-specific inputs.
- The workflow editor is catalog-driven after node creation and already has a schema-renderer fallback.

## Structural Defects

- `CanDoItAll.AgentFramework.Workflows.Abstractions` declares duplicate interfaces with zero consumers. Active catalog contracts remain in Common Core and active runtime contracts remain in Runtime under the `CanDoItAll.AgentFramework.Core` namespace. Workflows.Core therefore references Runtime and Common.Core, so the advertised abstraction boundary is fake.
- Executor registration has two truths: descriptor sources feed the catalog while `IWorkflowExecutor` registrations feed the invoker. Standard families repeat both registrations; plugins rebuild descriptors from manifests and lose executable defaults and simulation metadata.
- Existing architecture checkpoint tests preserve partial-class clusters and the inverted project graph rather than asserting behavior and dependency direction.
- `SourceIngestionWorkflowExecutor` is a five-file partial class that parses PDF, DOCX, HTML, ZIP, XLS, and XLSX itself. This duplicates ManagedCode.MarkItDown and spreadsheet services.
- `WorkspaceFileWorkflowExecutor` shares `IWorkspaceFileService`, but lacks exact `ListDirectory` semantics and uses reflection to interpret typed operation results.
- The command-process descriptor is planned and intentionally throws. Document-to-Markdown, image inspection, and image analysis are not workflow executors although runtime tools expose them.

## Lifecycle And Analytics Gaps

- Scheduler, project structure, API/UI, and a project-structure-specific agent tool can start workflows. There is no generic governed workflow agent tool.
- The Process editor exposes a Workflow executor kind, but the resolver rejects it as a non-agent. `WorkflowProcessExecutorBridge` is registered and unused.
- `WorkflowRuntimeManager.StartAsync` waits for backend completion before persisting run/events/artifacts, so no authoritative Running state or incremental progress exists and crashes can lose the whole run.
- Usage payloads omit reasoning/total-token detail and pricing provenance. Workflow run/node correlation is not consistently populated on provider observations.
- Executor-node usage is not copied into compiler progress events, while LLM-node usage is.
- Run snapshots and workflow UI expose state/counts, not duration, provider/model token totals, unknown-usage counts, or cost.

## UI And Plugin Gaps

- Quick-create settings and create-time mutation are hard-coded to five built-in executor IDs.
- `WorkflowExecutorCanvasCatalog` replaces the descriptor renderer key with a category-derived key.
- A schema-first inspector branch makes several specialized built-in settings editors unreachable.
- `ISettingsRendererSource` has no production registrations. The host does not enforce renderer trust or schema version.
- Plugin manifests can declare component type names and renderer keys, but arbitrary type activation would be unsafe. Gmail/Office365 descriptors already drift from their manifest-derived catalog entries.
- Workflow analytics currently counts a small loaded run page rather than querying complete usage data.

## Baseline Evidence

- Main CodeAnalytics snapshot: `snap-20260712155251-9c6f7b5e`, 46 projects, 1005 documents, 2828 types, 23523 members, 958 findings, 67 diagnostics.
- Focused executor snapshot: `snap-20260712154927-35ca25e8`, 28 projects, 519 documents, no cycles or blocking diagnostics.
- Baseline dependency cycles: two existing cycles inside `CanDoItAll.Modules.AgentFramework`; neither is in the intended change path.
- Components MCP: unavailable with `Transport closed`; this is a preparation-time validation gap, not permission to invent a parallel component system.
