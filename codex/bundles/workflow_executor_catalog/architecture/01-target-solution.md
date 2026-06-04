# Target Solution

## Architecture direction

Keep the current canonical structure:

- CanDoItAll workflow model remains the product persistence/editing model.
- MAF remains the execution adapter for in-process dynamic graphs.
- Workflow executors remain typed `IWorkflowExecutor` implementations.
- Plugin executors remain governed by descriptor metadata, permissions, approval policy, and audit observers.
- Durable production runtime remains future work.

## New target layers

### 1. Validation split

Introduce or formalize two validation phases:

1. `WorkflowGraphDefinitionValidator`  
   Validates graph shape, route definitions, node IDs, component references, shapes.

2. `WorkflowRuntimeCapabilityValidator`  
   Validates executor catalog, runtime backend availability, plugin availability, settings schema, approval/human policies.

This avoids DI/circularity problems and makes tests explicit.

### 2. Artifact content boundary

Add:

- `IWorkflowArtifactContentStore`
- `WorkflowArtifactContentWriteRequest`
- `WorkflowArtifactContentReadResult`
- persistent/workspace implementation
- API endpoint for artifact content retrieval
- UI link/open action

Artifact metadata and content must be consistent.

### 3. Executor families

Organize executor catalog into families:

- Workspace File/Folder
- Source Ingestion / Documents
- Data Transform
- Report / Markdown
- HTTP / Network
- Spreadsheet / Tables
- Project Structure
- Human / Approval
- Control / Delay / Batch
- Agent / Subworkflow
- Command / Host Tools

### 4. Helper nodes

Convert ambiguous helper node kinds into explicit runtime semantics:

- `Artifact` => implemented as artifact/write-reference executor or blocked until configured.
- `StrictLogic` / `Triage` => deterministic JSON route/transform nodes.
- `AgentStep` => agent call executor with permissions.
- `Subworkflow` => subworkflow call executor with recursion and timeout limits.
- `HumanInput` => retain current external-request behavior.
