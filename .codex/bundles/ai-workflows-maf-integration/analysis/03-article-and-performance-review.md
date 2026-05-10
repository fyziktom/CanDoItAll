# Article And Performance Review

## Article Source

- Official source reviewed: `https://devblogs.microsoft.com/dotnet/durable-workflows-in-microsoft-agent-framework/`.
- Local fallback exists but was not needed: `C:\Users\lucys\Downloads\devblog\Durable Workflows in the Microsoft Agent Framework - .NET Blog.html`.

## Guidance Added To Bundle

- In-process MAF workflow execution is acceptable for quick starts, local development, previews, and tests, but it loses state if the process exits.
- Durable production/long-running execution should evaluate and prefer `Microsoft.Agents.AI.DurableTask` backed by Durable Task Scheduler because it provides stateful execution, automatic checkpointing, distributed execution, long-running orchestration, and dashboard observability.
- The workflow definition should remain stable while the hosting/runtime changes. CanDoItAll should preserve this by compiling its workflow domain model to the same MAF `Workflow` graph, then selecting an execution backend.
- Use `ConfigureDurableOptions` when workflows and AI agents are hosted together so agents referenced by workflows are registered consistently. Use `ConfigureDurableWorkflows` only for workflow-only durable hosts.
- Treat Azure Functions hosting as an explicit architecture option. It can generate workflow run endpoints, durable orchestration/activity/entity functions, RequestPort response/status endpoints, and MCP tool triggers.
- CanDoItAll should still own product APIs, authorization, audit, workflow/process projections, artifacts, UI, and process-role integration even if MAF/Functions generated endpoints exist.

## MAF Durable Source References

- `C:\repositories\agent-framework\dotnet\src\Microsoft.Agents.AI.DurableTask\ServiceCollectionExtensions.cs`
- `C:\repositories\agent-framework\dotnet\src\Microsoft.Agents.AI.DurableTask\Workflows\IWorkflowClient.cs`
- `C:\repositories\agent-framework\dotnet\src\Microsoft.Agents.AI.DurableTask\Workflows\IWorkflowRun.cs`
- `C:\repositories\agent-framework\dotnet\src\Microsoft.Agents.AI.DurableTask\Workflows\IStreamingWorkflowRun.cs`
- `C:\repositories\agent-framework\dotnet\src\Microsoft.Agents.AI.DurableTask\Workflows\DurableWorkflowClient.cs`
- `C:\repositories\agent-framework\dotnet\src\Microsoft.Agents.AI.DurableTask\Workflows\DurableWorkflowOptions.cs`
- `C:\repositories\agent-framework\dotnet\src\Microsoft.Agents.AI.DurableTask\Workflows\DurableWorkflowRunner.cs`
- `C:\repositories\agent-framework\dotnet\src\Microsoft.Agents.AI.DurableTask\Workflows\DurableStreamingWorkflowRun.cs`
- `C:\repositories\agent-framework\dotnet\src\Microsoft.Agents.AI.DurableTask\Workflows\PendingRequestPortStatus.cs`
- `C:\repositories\agent-framework\dotnet\src\Microsoft.Agents.AI.Hosting.AzureFunctions\Workflows\DurableWorkflowOptionsExtensions.cs`
- `C:\repositories\agent-framework\dotnet\src\Microsoft.Agents.AI.Hosting.AzureFunctions\Workflows\DurableWorkflowsFunctionMetadataTransformer.cs`
- `C:\repositories\agent-framework\dotnet\src\Microsoft.Agents.AI.Hosting.AzureFunctions\BuiltInFunctions.cs`

## Performance Scan Scope

- Targeted existing C# files under:
  - `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Models`
  - `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Core`
  - `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Maf`
  - `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Persistence`
  - `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework`
  - `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes`
  - `C:\repositories\CanDoItAll\src\CanDoItAll.Web\Api`
- Excluded generated/build/artifact folders: `bin`, `obj`, `.codex`, `.artifacts`.
- Files scanned: 312.

## Scan Execution Checklist

- `\.IndexOf\("`: 2 hits.
- `\.Substring\(`: 6 hits.
- `\.(StartsWith|EndsWith|Contains)\s*\(`: 1311 hits.
- `\.ToLower\(\)|\.ToUpper\(\)`: 0 hits.
- `\.Replace\(`: 136 hits.
- `params `: 24 hits.
- `\.Select\(|\.Where\(|\.OrderBy\(|\.GroupBy\(`: 1896 hits.
- `\.Any\(|\.All\(`: 261 hits.
- `new Dictionary<|new List<`: 326 hits.
- `static readonly Dictionary<`: 0 hits.
- `RegexOptions\.Compiled`: 2 hits.
- `new Regex\(`: 0 hits.
- `GeneratedRegex`: 0 hits.
- `\b(public|internal)\s+class\s+`: 0 hits.
- `\bsealed\s+class\s+`: 315 hits.
- `:\s*IEquatable`: 0 hits.
- Refined sync-over-async scan `\.Result\b|\.Wait\(|\.GetAwaiter\(\)\.GetResult\(`: 0 hits.
- `ValueTask`: 29 hits.

## Findings For Bundle Planning

- No critical sync-over-async finding was found in the targeted existing workflow-adjacent files.
- Several scan hits are broad signals rather than defects because many are UI/projection/query code, not proven hot paths. The implementation bundle must still guard new workflow runtime code against LINQ-heavy event loops, allocation-heavy polling/status paths, and string slicing in serializers/parsers.
- New workflow runtime code should explicitly avoid sync-over-async, repeated `ValueTask` awaits, ad hoc regex, culture-sensitive string operations, and avoidable allocations in event streaming/status polling/human-in-loop response handling.
- Durable Task integration introduces serialization and orchestration replay constraints. Implementation agents must follow MAF/DurableTask patterns and avoid blocking calls, non-deterministic orchestration code, and excessive payload copying in durable hot paths.

## Bundle Repairs Made

- Added durable workflow requirements RQ-022 through RQ-026.
- Updated architecture to prefer MAF DurableTask/DTS for durable production execution.
- Updated subbundles 01, 02, 07, and 08 with DurableTask, DTS, Azure Functions hosting, RequestPort endpoint, MCP exposure, and performance review gates.
