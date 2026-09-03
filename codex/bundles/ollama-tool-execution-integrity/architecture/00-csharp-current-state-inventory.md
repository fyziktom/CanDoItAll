# C# Current State Inventory

## Evidence and scope

Baseline source commit: 40c55418e8a5acd870c5ddc1175035d6da1153a6. Source tree was clean before bundle creation. No production edits were made.

[CodeAnalytics evidence](../analysis/codeanalytics-summary.json) records snapshot snap-20260903162319-aa914253, six selected projects, 420 documents, and four DI collector diagnostics. No project cycle was found within that scope; this is not a whole-solution assertion. Factory registrations not statically understood by the collector are not demonstrated broken DI.

[Project references](../analysis/project-references.json) contain the complete literal project references and packages for the seven principal owners. These references were read from csproj files, independently of scope-filtered analytics. Existing Workbench module/type cycles are baseline context, not new scope.

| Owner / type | Current responsibilities | Evidence and decision |
|---|---|---|
| MafAgentRuntime | Composition facade for runtime services | 146 lines. Preserve its facade role; do not put repair/completion policy here. |
| MafRuntimeAgentFactory | Tool construction, authorization, approval, invocation middleware, traces and SDK agent setup | 898 lines, 25 members, 8 constructor parameters. Extract only the touched invocation adaptation responsibility into a cohesive top-level collaborator. |
| MafStreamingTurnExecutor | Streaming, session, continuation and finalizer orchestration | 1,307 lines, 20 members. Consume the common assessment result rather than invent an independent status path. |
| MafRuntimeToolInvocationResultClassifier | Reflection/string interpretation of domain, workspace and MCP results | 568 lines, 21 members. Typed adapters and an explicit unknown result replace implicit-success behavior in the touched path. |
| ProjectStructureAgentRuntimeToolProvider | Catalog for 55 project tools and tool builder | 3,439 lines; nested builder 127 members/18 parameters. Asset family and operation execution are the relevant seams. No 55-tool rewrite. |
| MafProviderAgentFactory | SDK-specific agent/client construction | Native Ollama and OpenAI-compatible clients stay here. No domain mutation knowledge. |
| AgentFrameworkWorkspaceExecutionService | Durable run lifecycle and terminal state | Existing partial cluster orchestrates application policies. New policy must be a top-level cohesive type, not another partial. |
| ProjectStructureAgentService | Authorization, parent checks, managed storage and node metadata | Remains the domain/application owner of registration. Do not copy its storage logic into the agent runtime. |
| AgentChatContextInvocationFactory / notification hub | Scoped completion notifications | Extend effect awareness without moving canonical graph loading into runtime/provider code. |
| ProjectStructureAgentChatContextProvider / ProjectStructurePage | Held context and canvas refresh | Existing source/project matching and reload path remain; test effect-driven refresh and disposal. |

## Contracts already available

AgentRuntimeResponse already exposes ToolInvocationTraces through Runtime.Abstractions. ToolExecutionReceiptRecord and ToolExecutionSideEffectMode live in Models. IAgentToolFailure supplies safe application errors. These are the first reuse points; introduce only missing typed values and a bounded safe projection.

Runtime.Abstractions depends on Models and ProviderHistory.Abstractions, not Core's project. Its historical namespace imports are not a project reference and must not justify adding a reverse reference. Models has no provider SDK package. Core depends on Providers and runtime ports; Providers has no SDK packages. SDK packages are currently owned by Maf.

## Review boundary

Source inspection and an isolated SDK probe are analysis, not product build/test proof. Large files and unrelated cycles justify scrutiny, not automatic refactoring. Architecture closure requires the actual implementation diff and changed dependency graph.

## Package baseline addendum

The MAF family is pinned at 1.18.0 (A2A preview 1.18.0-preview.260818.1), with direct MEAI 10.8.0 and root Microsoft.Extensions 10.0.10 pins. MAF 1.20 requires MEAI 10.9 and Microsoft.Extensions foundation 10.0.11. The isolated 1.20 probe shows the malformed argument defect remains. SB00 upgrades and freezes the SDK baseline before application fixes.
