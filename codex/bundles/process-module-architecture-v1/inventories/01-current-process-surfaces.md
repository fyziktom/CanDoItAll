# Current Process Surface Inventory

## Strong Reuse Candidates

| Surface | Current Source | Reuse Recommendation |
| --- | --- | --- |
| Process UI workspace direction | `repo://src/CanDoItAll.Modules.Processes/Components/ProcessWorkspace.razor` and partials | Keep as UX reference. Rebuild against new projections. |
| Live processes UX direction | `repo://src/CanDoItAll.Modules.Processes/Components/LiveProcessesDashboard.razor` | Keep UX and interaction model. Replace data source with snapshot projections. |
| Canvas visual model | `repo://src/CanDoItAll.Modules.Processes/Canvas` | Keep ideas for roles, steps, artifacts, runtime nodes, branch routers. Rebind to new read models. |
| Template pack file layout | `repo://Templates/Processes` | Keep as migration input. Convert to canonical schema with component refs and migrations. |
| Template loader concepts | `repo://src/CanDoItAll.Modules.Processes/Templates/ProcessTemplatePackLoader.cs` | Rebuild in `CanDoItAll.Processes.Templates`; keep JSON-first direction. |
| Pure artifact matching rules | `repo://src/CanDoItAll.Processes.Core/Artifacts/ProcessArtifactExpectationMatcher.cs` | Keep/adapt into new core after model review. |
| Pure route diagnostics | `repo://src/CanDoItAll.Processes.Core/Routing` | Use as reference for explicit route decisions; replace stage list with strategy-based scheduler if needed. |
| Subprocess lifecycle status mapping | `repo://src/CanDoItAll.Processes.Core/Subprocess/ProcessSubprocessLifecycleRules.cs` | Keep/adapt as small rule object. |
| Recovery packet concepts | `repo://src/CanDoItAll.Modules.Processes/Automation/Recovery/AgentRecoveryModels.cs` | Preserve ideas behind generic recovery strategy contracts. |
| Verification driver diagnostics | `repo://src/CanDoItAll.Processes.Drivers.*` | Preserve as reference and test corpus. Refactor into broader driver capability model. |
| Existing tests | `repo://tests` | Use as behavioral reference. Replace brittle static tests with contract and integration tests. |

## High-Risk Reuse Candidates

| Surface | Risk | Recommendation |
| --- | --- | --- |
| `ProcessRunAutomationDispatchService` | Central partial service mixes dispatcher, domain rules, recovery, prompts, artifact projection, validation, provider fallback, and finalization. | Do not wrap. Copy as reference, then rebuild with explicit runtime/dispatcher/strategy boundaries. |
| `ProcessesService.StartRunAsync` | Good transactional behavior but composes and persists directly inside service. | Extract concepts into builder/persister flow. |
| Current branch text heuristics | Fragile and domain-dependent. | Replace with typed branch definitions and strategy-selected outcomes. |
| Current observation service | Useful projections but query-built and cache-first. | Replace with event-first projection pipeline. |
| Current template sidecars | Useful human reference but not canonical. | Generate Markdown/Mermaid on demand from JSON. |

## Surfaces To Remove On Rewrite Branch After Archival

- `src/CanDoItAll.Modules.Processes`
- `src/CanDoItAll.Processes.Core`
- `src/CanDoItAll.Processes.Contracts`
- `src/CanDoItAll.Processes.Drivers.*`
- Process-specific tests in unit, component, integration, and Playwright projects
- Current template projections that represent current-module compatibility rather than canonical source

Removal must happen only after the old implementation is copied into the rewrite bundle reference area.

