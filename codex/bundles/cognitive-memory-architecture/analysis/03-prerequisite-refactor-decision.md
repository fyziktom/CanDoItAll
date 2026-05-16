# Prerequisite Refactor Decision

## Decision

Yes, existing code should be refactored before Cognitive Memory implementation starts.

The required prerequisite is not a large rewrite. It is a boundary refactor that prevents Cognitive Memory from being hardwired into current private MAF internals and prevents high-volume source ingestion from depending on UI-oriented or EF-entity-specific read paths.

## Why This Matters

The cognitive memory module will touch agent context, workflow executors, process reflection, workbench nodes, source snapshots, recall traces, projection state, and high-volume background jobs. If implementation starts by adding logic directly to the existing MAF context builder or by reading Workbench/Process tables wherever convenient, the memory system will become tightly coupled to unstable implementation details.

The architecture needs source adapters and context contributors as first-class contracts because memory is long-lived and cross-cutting. MAF should consume memory context; it should not own memory policy or durable records.

## Evidence

| Evidence | Source | Impact |
|---|---|---|
| MAF context provider composition is a private nested builder with hardcoded RAG/static/Mem0 handling. | `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Maf\Runtime\Capabilities\MafAgentRuntime.Capabilities.Context.cs` | Cognitive Memory would otherwise require direct edits in private MAF internals. |
| Current workspace memory provider is private and keyword-scored. | `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Maf\Runtime\Capabilities\MafAgentRuntime.Capabilities.Context.cs` | It is compatibility fallback, not an extensible cognitive recall boundary. |
| `CanDoItAll.AgentFramework.Maf` already references Workbench, Processes, Projects, Security, Workspace, and Tools.Documents. | `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Maf\CanDoItAll.AgentFramework.Maf.csproj` | Adding durable memory concerns here would deepen domain coupling. |
| `IProjectStructureRuntimeGateway` is agent-command oriented and lacks high-volume source cursor/hash semantics. | `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Core\ProjectStructure\ProjectStructureRuntimeGatewayContracts.cs` | Memory ingestion needs stable source snapshots, not only agent node summaries. |
| Workbench has rich source records and projection data, but the source snapshot boundary is not explicit. | `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Workbench\ProjectWorkbenchModels.cs` | Memory source adapters need deterministic item identity, hashes, links, layout, references, and timestamps. |

## Required Refactor Bundle

Create, implement, validate, and approve `codex/bundles/cognitive-memory-prerequisite-boundaries`.

Required refactor goals:

- Add a MAF context contribution extension point that lets modules register context contributors without modifying private MAF context-provider code for every new memory source.
- Add read-only source snapshot contracts for project structure, process runtime, and workflow runtime that expose stable source item ids, source hashes, update cursors, provenance, layout, links, and storage references.
- Keep the new contracts in low-level abstractions or module-owned adapter interfaces so Cognitive Memory can depend on contracts, not private UI models or EF persistence details.
- Preserve existing behavior and keep `WorkspaceMemoryContextProvider` as compatibility fallback.

## Impact Projected Into Cognitive Memory

- `subbundles/00-prerequisite-boundary-gate` has passed; Cognitive Memory implementation can start only by consuming the approved boundaries.
- `codex/bundles/cognitive-memory-boundary-hardening` has also passed; source ingestion, recall, and MAF integration must consume the hardened paging/cursor, redaction/hash, and contributor-trace contracts.
- `subbundles/02-workbench-and-source-ingestion` consumes the source snapshot contracts instead of direct table reads.
- `subbundles/07-maf-workflow-integration` consumes the context contribution boundary and retained contributor traces instead of editing private MAF internals.
- `subbundles/06-consolidation-engine` consumes source cursors and restricted hash policies, making idle processing resumable and incremental without projecting raw sensitive integrity hashes.

## Closure Evidence

- `AgentContextContributionContracts.cs` defines the generic MAF context contribution contract and result model.
- `AgentContextContributionContracts.cs` also defines contributor trace records and a trace collector for future recall/context audit.
- `MemorySourceSnapshotContracts.cs` defines source snapshot identity, typed cursor failure, page/hash scope, restricted hash policy, provenance, layout, permission, links, references, and storage contracts.
- Workbench, Process, and Workflow modules now provide read-only source adapters without referencing Cognitive Memory.
- Process and Workflow source adapters now page through query-backed source slices. Workbench project-structure paging remains a documented bounded-source exception because the canvas assembly service currently returns the complete surface.
- `dotnet build .\CanDoItAll.slnx --no-restore` passed with 0 warnings and 0 errors.
- Targeted unit and integration tests for context contributors, Workbench snapshots, and runtime evidence providers passed.
- Boundary-hardening targeted tests and completed-stage bundle validation passed.

## Non-Goals

- Do not build Cognitive Memory in the prerequisite bundle.
- Do not change Workbench UI behavior.
- Do not redesign process/workflow persistence.
- Do not replace the existing RAG or SemanticCompletion repositories.
