# Current State

## Existing Tool Path

- The agent-visible tool is `workspace_convert_document`.
- MAF exposes it through `WorkspaceRuntimePlugin.ConvertDocumentToMarkdown`.
- `WorkspaceRuntimePlugin` delegates to `IWorkspaceArtifactToolService`.
- `WorkspaceArtifactToolService.ConvertDocumentToMarkdown` currently calls `IWorkspaceCommandExecutionService.ConvertDocumentWithMarkItDown`.
- The command service builds a local process plan that runs `python -m markitdown`.

## Root Cause

The conversion behavior is not implemented as a document-domain tool. It is implemented as a workspace process recipe. That creates avoidable runtime cost, environment fragility, and poor testability:

- Python package availability becomes a runtime prerequisite.
- Conversion is only validated indirectly through process execution.
- The workspace command layer owns document conversion details that belong in `CanDoItAll.Tools.Documents`.
- DI fallback in `MafRuntimeDependencyResolver` can create `WorkspaceArtifactToolService` directly, so converter registration must be part of the fallback path too.

## Scope Boundary

The correct split is:

- Core owns SDK-free workspace contracts and receipts.
- `CanDoItAll.Tools.Documents` owns document conversion implementation and third-party document SDK references.
- Hosting/module composition wires the implementation.
- MAF runtime only exposes the already-composed tool.

## Validation Constraint

The existing Financial Strategist seed has `projectStructure.canRead=true` and `projectStructure.canWrite=false`. It has the document conversion capability and business-analysis workspace artifact permissions, but it cannot create project-structure nodes unless a separate permission change is approved.

