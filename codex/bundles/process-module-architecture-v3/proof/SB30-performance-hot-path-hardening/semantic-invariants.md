# SB30 Semantic Invariants

## Invariant ID: SB30-PERF-001

Expected behavior: Process definition and template catalog projections do not rebuild stable source item lists on every query, and their search/sort behavior is independent of the host culture.

Disallowed shallow implementation: Keep rebuilding catalog/source lists per request, or switch only one comparison call while leaving title/summary search locale-dependent.

Passing tests: `ProcessDefinitionCatalogProjectionTests.Catalog_query_uses_ordinal_search_independent_of_current_culture` and `ProcessDefinitionCatalogProjectionTests.Template_catalog_query_uses_ordinal_search_independent_of_current_culture`.

Changed source files:
- `repo://src/CanDoItAll.Processes.Application/ProcessDefinitionCatalogProjectionService.cs`
- `repo://src/CanDoItAll.Processes.Application/ProcessTemplateCatalogProjectionService.cs`
- `repo://tests/CanDoItAll.Tests.Unit/ProcessDefinitionCatalogProjectionTests.cs`

## Invariant ID: SB30-PERF-002

Expected behavior: Runtime shell metric buckets and tool-usage projections are computed through bounded explicit accumulators instead of grouping chains that repeatedly enumerate each group.

Disallowed shallow implementation: Keep LINQ `GroupBy`/`Count`/`Max` chains and only rename helper methods.

Passing test: `ProcessProjectionPipelineTests.Shell_projection_aggregates_metric_buckets_and_tool_usage_deterministically`.

Changed source files:
- `repo://src/CanDoItAll.Processes.Application/ProcessWorkspaceShellProjectionService.cs`
- `repo://tests/CanDoItAll.Tests.Unit/ProcessProjectionPipelineTests.cs`

## Invariant ID: SB30-PERF-003

Expected behavior: Process mock and scenario harness static literal regexes use source-generated regex methods, not runtime compiled regex construction.

Disallowed shallow implementation: Keep `RegexOptions.Compiled` and add wrapper methods around static regex fields.

Passing proof: `bundle://proof/SB30-performance-hot-path-hardening/transcripts/static-regression-scans.txt` and `bundle://proof/SB30-performance-hot-path-hardening/transcripts/build-agentframework-module.txt`.

Changed source files:
- `repo://src/CanDoItAll.Modules.AgentFramework/Hosting/ProcessMockAgentRuntime.cs`
- `repo://src/CanDoItAll.Modules.AgentFramework/Hosting/ProcessMockAgentRuntime.BranchOutcomes.cs`
- `repo://src/CanDoItAll.Modules.AgentFramework/Hosting/ProcessMockAgentRuntime.PromptArtifacts.cs`
- `repo://src/CanDoItAll.Modules.AgentFramework/Hosting/ProcessMockAgentRuntime.SessionState.cs`
- `repo://src/CanDoItAll.Modules.AgentFramework/Hosting/ScenarioHarnessAgentRuntime.cs`

## Production Behavior Artifact Matrix

| Behavior | Producer | Consumer | Lifecycle | Negative or guard proof |
| --- | --- | --- | --- | --- |
| Definition catalog cache and ordinal filtering | `ProcessDefinitionCatalogProjectionService` | `/processes` definition catalog projection | Service instance lazily builds stable catalog items from the template pack and filters with ordinal comparisons | Turkish-culture test proves `INDIGO` title search remains deterministic |
| Template catalog cache and ordinal filtering | `ProcessTemplateCatalogProjectionService` | Template library projection and import command path | Service instance lazily builds template source items and reuses them for query/import operations | Turkish-culture test proves template title search remains deterministic |
| Runtime dashboard aggregation | `ProcessWorkspaceShellProjectionService` | Process shell/live dashboard analytics | Events are added to metric/tool accumulators, then sorted into projection DTOs | Projection test verifies bucket timestamps, counts, durations, and usage ordering from real projected events |
| Source-generated parser regexes | Process mock and scenario harness runtimes | Deterministic process provider flows | Compiler-generated regex methods are called from parser paths | Static scan proves `RegexOptions.Compiled` and `new Regex(` are absent in modified process harness scope |

## Residual Risk

No throughput benchmark was captured. If process catalogs grow into thousands of definitions or runtime dashboards become high-frequency polling surfaces, add BenchmarkDotNet or production telemetry around catalog query latency and shell projection allocation.

