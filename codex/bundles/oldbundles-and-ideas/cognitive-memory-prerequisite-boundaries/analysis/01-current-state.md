# Current State

## MAF Context

`MafAgentRuntime.Capabilities.Context.cs` contains private context provider composition for RAG, static context, Mem0, and workspace memory. The workspace memory provider is private and keyword-scored over existing agent memory records. This is useful as compatibility behavior, but it is not a stable extension point for a durable cognitive memory module.

`CanDoItAll.AgentFramework.Maf.csproj` already references several domain modules. Adding Cognitive Memory directly there would deepen coupling and make the MAF adapter responsible for durable memory concerns.

## Workbench And Project Structure

`IProjectStructureRuntimeGateway` is useful for agent-facing project-structure operations, but it is not a high-volume memory source contract. It lacks explicit source item hashes, scan cursors, source snapshot identity, storage references, and layout/source version metadata.

`WorkbenchProjectStructureRuntimeGateway` maps Workbench project structure into agent summaries. It should not become the canonical ingestion path for Cognitive Memory unless it is complemented by a source snapshot contract.

Workbench stores rich project objects, links, node references, bindings, view states, lifecycle events, and metadata. It stores X/Y coordinates directly and can carry Z/layout extension data through metadata for V1.

## Process And Workflow Sources

`ProcessRuntimeModels.cs` contains durable runtime evidence: runs, step runs, decisions, artifacts, journal entries, conformance observations, improvement candidates, assignments, work briefs, and workflow links.

`WorkflowExecutorContracts.cs` defines executor, invocation, event, artifact, external request, and run store contracts. These are good sources for episodic and procedural memory, but Cognitive Memory should consume them through a stable source/event boundary rather than direct persistence details.

## Architectural Conclusion

The prerequisite should be a boundary refactor, not a large feature rewrite. The smallest correct change is to introduce extension/read-model contracts that future Cognitive Memory implementation can consume cleanly.
