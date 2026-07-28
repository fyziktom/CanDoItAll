# SB05 CodeAnalytics Direct Transcript

## Command metadata

- Command: `code_analytics_recent_snapshots_list`;
  `code_analytics_solution_inventory_get`;
  `code_analytics_dependencies_get`
- ExitCode: 0
- Exit-code provenance: normalized successful outcome for direct MCP results whose
  recorded `ok` value is `true`; this is not a shell-process exit code
- Working directory: `C:\repositories\CanDoItAll`

## Snapshot lookup

- Class: `Direct`
- Tool: `code_analytics_recent_snapshots_list`
- Timestamp: `2026-07-28T00:21:49.2790841+00:00`
- Correlation: `code-analytics_86c057b31a2d457d96b4c13be2c261df`
- Snapshot: `snap-20260727233256-654bc9d9`
- Solution: `C:\repositories\CanDoItAll\CanDoItAll.slnx`
- Created: `2026-07-27T23:32:56.1253321+00:00`
- Findings: 1,182
- Diagnostics: 216
- From cache: false
- Result: `ok: true`

## Solution inventory

- Class: `Direct`
- Tool: `code_analytics_solution_inventory_get`
- Timestamp: `2026-07-28T00:21:49.7661722+00:00`
- Correlation: `code-analytics_f7f89ce129474941a335669fc1ed18d6`
- Projects: 12
- Documents: 963
- Result: `ok: true`

Relevant direct project references:

```text
SharedKernel -> []
AgentFramework.Models -> [SharedKernel]
AgentFramework.Core -> [AgentFramework.Models, SharedKernel]
AgentFramework.Persistence -> [AgentFramework.Core, AgentFramework.Models, SharedKernel]
AgentFramework.Maf -> [AgentFramework.Core, AgentFramework.Models, Modules.Workspace, SharedKernel]
Processes.Projections -> []
Processes.Application -> [Processes.Projections]
Modules.AgentFramework -> [Core, Maf, Models, Persistence, Infrastructure, Modules.Workbench, Modules.Workspace, SharedKernel]
Modules.Processes -> [Core, Models, Infrastructure, Modules.AgentFramework, Processes.Application, Processes.Projections, SharedKernel]
```

The project graph is acyclic.

## Dependency/cycle query

- Class: `Direct`
- Tool: `code_analytics_dependencies_get`
- Search: `CanDoItAll.AgentFramework.Core`
- Timestamp: `2026-07-28T00:21:50.2672919+00:00`
- Correlation: `code-analytics_e1027091e995486eb75d9e5140212bb1`
- Filtered dependencies: 13
- Result: `ok: true`

Reported non-project cycles:

- 3 module cycles: Infrastructure, Modules.AgentFramework Hosting/module, and
  Modules.Workbench.
- 2 nested-type cycles:
  `AgentReferenceDataCache`/nested cache entry and
  `ImageGenerationAgentRuntimeToolProvider`/nested builder.

The review records these rather than claiming a cycle-free snapshot.
