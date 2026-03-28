# Current State

## Existing Strengths

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Projects\ProjectModels.cs`
  - `ProjectsService` already persists projects and multi-parent subproject links, validates cycles, and exposes hierarchy queries.
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\ProjectWorkbenchModels.cs`
  - `ProjectWorkbenchService` already owns project-structure nodes, links, media-backed asset nodes, metadata validation, hierarchy projection, and structure/calendar surfaces.
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workspace\WorkspaceModels.cs`
  - `WorkspaceService` already persists centralized settings and provider profiles and is the least disruptive place to add agent-policy settings.
- `C:\repositories\CanDoItAll\src\CanDoItAll.Web\Program.cs`
  - The web app already hosts the DB, managed file store, and module registrations, so it is the natural central HTTP authority.
- `C:\repositories\CanDoItAll\src\CanDoItAll.Mcp.Core\Concurrency\ResourceMutationGate.cs`
  - Existing MCP code already has a local resource-mutation primitive that can be adapted or mirrored for cross-process locking semantics.
- `C:\repositories\CanDoItAll\src\CanDoItAll.Mcp.DotNetWatch\Program.cs`
  - Existing MCP infrastructure already demonstrates stdio tools, structured envelopes, settings validation, backend HTTP routes, idempotent replay, and shadow-install patterns.
- `C:\repositories\CanDoItAll\tools\Reinstall-CanDoItAllMcps.ps1`
  - Existing rollout tooling already publishes MCP binaries, syncs skills, and updates local MCP config.

## Current Gaps

- There is no project-structure-specific MCP server.
- There are no HTTP endpoints in CanDoItAll web for external agent access to projects or workbench data.
- There is no central agent policy or approval threshold model in workspace settings.
- There is no central cross-machine lease or reservation service for project-structure edits.
- There is no dedicated checklist/query service that computes unfinished items with prerequisites and priority propagation.
- There is no central knowledge-guidance provider for project-management best practices.
- There is no setup UX or generated instructions for connecting a remote project-structure MCP to the main CanDoItAll machine.
- Existing tests prove workbench operations locally, but not a remote MCP flow against a central API.

## Architecture Pressure Points

- Reusing `ProjectWorkbenchService` directly inside a remote MCP would break the stated deployment model because remote machines would not share the main machine DB and files.
- Adding a new parallel persistence model for MCP-only project structure would create drift against the UI and existing services.
- Locking must be central, not local to an MCP process, otherwise multiple machines still collide.
- The settings UI and rollout story must be good enough that other workstations can configure the new MCP without manual hidden steps.

## Existing Proof That Reduces Risk

- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\ProjectWorkbenchServiceIntegrationTests.cs`
  - Existing integration tests already prove node creation, metadata updates, asset persistence, reparenting, deletion, and hierarchy projection.
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Mcp.Components.Tests\ComponentsToolsTests.cs`
  - Existing MCP tool tests already prove the repo pattern for direct tool invocation without spinning a full client.
- `C:\repositories\CanDoItAll\project-hierarchy-bundle-1\README.md`
  - The repo already adopted a bundle-driven workflow for hierarchy work, which gives a precedent for proof quality and validation gates.
