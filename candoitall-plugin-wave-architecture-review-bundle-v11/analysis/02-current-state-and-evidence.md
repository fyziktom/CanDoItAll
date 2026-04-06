# Current state and evidence

## Runtime and orchestration baseline
The repo already contains useful building blocks:

- durable connector command records and audit records,
- a user-facing automation workspace,
- background job records for UI diagnostics,
- connector manifests and open-world editor support,
- explicit projection repair for stale workbench projections.

However, the platform still lacks the runtime plane that actually wakes work up automatically.

## Evidence-backed gaps

### In-memory-only background job queue
`IBackgroundJobQueue` exposes `EnqueueAsync(...)` and `DequeueAsync(...)`, but the concrete queue is still `InMemoryBackgroundJobQueue` backed by `Channel<T>`. The DI registration is singleton in-memory queue + scoped tracker.

Relevant files:
- `src/CanDoItAll.Infrastructure/BackgroundJobs/BackgroundJobs.cs:15-20`
- `src/CanDoItAll.Infrastructure/BackgroundJobs/BackgroundJobs.cs:93-101`
- `src/CanDoItAll.Infrastructure/DependencyInjection/InfrastructureServiceCollectionExtensions.cs:98-100`

### No dequeue consumer
The current repo baseline only shows the interface declaration and the concrete implementation of `DequeueAsync(...)`. No runtime worker consumes it.

Baseline search:
- `inventories/05-runtime-gap-search-baseline.txt`

### Connector outbox processor exists but is not runtime-driven
`ConnectorOutboxService.ProcessPendingAsync(...)` exists, but the current repo search baseline shows no caller.

Relevant file:
- `src/CanDoItAll.Modules.Workspace/ConnectorOutboxService.cs:326-354`

### Singular automation signal provider is not open-world enough
`AutomationWorkspaceService` depends on a single `IAutomationSignalProvider`.
The automation module registers a default null provider via `TryAddScoped`, while CRM/HR registers its own provider via `AddScoped`.
That shape is not a real multi-source aggregation seam for a future plugin ecosystem.

Relevant files:
- `src/CanDoItAll.Modules.Automation/AutomationModels.cs:10-24`
- `src/CanDoItAll.Modules.Automation/AutomationModuleServiceCollectionExtensions.cs:9-13`
- `src/CanDoItAll.Modules.CrmHr/CrmHrModuleServiceCollectionExtensions.cs:9-21`

### “Background jobs” are mostly inline today
In `PromptFactoryService`, the tracker creates and updates a job record, but the actual export/send work still executes inline in the request flow instead of being drained by a worker.

Relevant file:
- `src/CanDoItAll.Modules.Factory/PromptFactoryService.cs:688-721`
- `src/CanDoItAll.Modules.Factory/PromptFactoryService.cs:744-771`

## Advisory leftovers
These are not phase11 blockers by themselves, but they remain visible maintenance debt:

- marker compatibility fallback from metadata: `src/CanDoItAll.Modules.Workbench/ProjectStructureAssemblyService.cs:77-82`
- reference compatibility fallback from metadata: `src/CanDoItAll.Modules.Workbench/ProjectNodeBindings.cs:391-395`
- hotspot: `src/CanDoItAll.Modules.CrmHr/CrmHrServices.cs` (~4969 lines)
- hotspot: `src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs` (~1147 lines)
