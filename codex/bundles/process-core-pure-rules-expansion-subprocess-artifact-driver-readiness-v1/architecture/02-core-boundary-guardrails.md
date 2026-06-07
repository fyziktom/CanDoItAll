# Core Boundary Guardrails

## Core allow-list for this bundle
- `CanDoItAll.Processes.Core.Routing`
- `CanDoItAll.Processes.Core.Subprocess`
- `CanDoItAll.Processes.Core.Artifacts`

## Core forbidden tokens
The implementation must fail if Core source contains:
- `Microsoft.EntityFrameworkCore`
- `DbContext`
- `AppDbContext`
- `CanDoItAll.Modules`
- `CanDoItAll.Infrastructure`
- `CanDoItAll.AgentFramework`
- `IStorage`
- `StoragePlacement`
- `Workspace`
- `File.`
- `Directory.`
- `IServiceScopeFactory`
- `ILogger<ProcessRunAutomationDispatchService>`
- `TransitionStep`
- `DispatchClaim`
- `Finalizer`
- `IProcessDriverPack`
- `ProcessDriverRegistry`
- `DriverPack`

## Module-local side effects
These remain in `CanDoItAll.Modules.Processes`:
- Candidate hydration.
- Technical-agent binding.
- Recovery directive query.
- Project-structure access mutation.
- Claim lifecycle.
- Route handlers and services.
- Subprocess child-run observation.
- Subprocess projection persistence and gap journals.
- Artifact projection writes.
- Validation orchestration.
- Execution/retry/provider repair.
- Finalizer application.
