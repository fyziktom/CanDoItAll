# Current State

## Bundle Format Status

- The legacy bundle was rich in content but stale relative to the current bundle-workflow contract.
- The old root structure used `00-context`, `01-workbooks`, `02-architecture`, `03-subbundles`, `04-codex`, and `05-manifest`.
- The latest validator requires `inputs`, `analysis`, `requirements`, `architecture`, `plan`, `traceability`, `shared-prompts`, `subbundles`, `reviews`, and initiative-specific `inventories` plus `templates`.
- The first validator pass failed because none of those new sections existed and the root `README.md` lacked `## Validation Summary`.

## Main Solution Evidence

- Snapshot used: `snap-20260409084912-d225a84b`.
- `CanDoItAll` now contains the shipped `CanDoItAll.Modules.Processes` module, process workspace UI, realistic seed service, runtime analytics, and process-canvas remediation from phase05 and phase06.
- Existing module and infrastructure patterns already support adding a new local MCP server through shared composition, shared `AppDbContext`, and runtime database-profile switching.

## Canonical Ownership Already Present In The Repo

- CRM-HR already owns durable AI identity through `AiAgentProfile` in `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CrmHr\CrmHrBusinessModels.cs`.
- Workspace already owns provider truth through `ProviderProfile` in `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workspace\WorkspaceModels.cs`.
- Workbench already exposes project-object seeding and structure assembly patterns through `ProjectWorkbenchService` in `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\ProjectWorkbenchModels.cs`.
- Infrastructure already exposes managed artifact storage and storage-placement seams through `IManagedArtifactStore` and `StoragePlacementService`.
- Processes already expose the canonical authoring and runtime service surface through `ProcessesService`, including definition listing, editor loading, save/publish/delete, run listing, analytics, artifact recording, and import/export.

## MCP Surface Status

- `CanDoItAll.Mcp.ProjectStructure`, `CanDoItAll.Mcp.Components`, `CanDoItAll.Mcp.CodeAnalytics`, `CanDoItAll.Mcp.DotNetWatch`, and `CanDoItAll.Mcp.SshOps` already define the local MCP installation and Codex config patterns used in this repo.
- `CanDoItAll.Mcp.ProjectStructure` is remote-HTTP-backed and depends on a project-structure agent API plus tokenized settings.
- The process module currently has no matching MCP surface, no dedicated local install script, and no reinstall-script wiring.
- The simplest maintainable fit for the new request is a local stdio MCP over canonical process services and the existing database-profile bootstrap path, not a new remote process-agent API.

## Cross-Repo Duplication Risk

- Snapshot used for `CanDoItAll.AgentFramework`: `snap-20260409084922-ab80e592`.
- `CanDoItAll.AgentFramework.Models.ProviderProfile` duplicates provider-shape concerns already present in `CanDoItAll.Modules.Workspace.ProviderProfile`.
- `AgentFrameworkWorkspaceService` currently manages agent definitions, provider bindings, chat sessions, execution logs, memory, and metrics inside runtime-side workspace documents.
- That runtime-side scope is useful for research, but dangerous as production truth if the process module does not lock canonical ownership now.

## IPFS Seam Status

- The IPFS repository is present and contains `CanDoItAll.IPFS.Client`, `CanDoItAll.IPFS.Engine`, and `CanDoItAll.IPFS.NodeControl`.
- CodeAnalytics could not build a clean Roslyn snapshot for `CanDoItAll.IPFS` because the workspace loader reported a duplicate project-key issue.
- Shell inspection still confirmed that the repo already provides the pieces needed for a future typed evidence-storage seam:
  client, embedded engine, and a control surface.

## Bundle Gaps Repaired By This Pass

- Missing validator structure
- Missing phase-gate and repair-bundle mechanics
- Missing explicit development/test seed strategy
- Missing explicit mapping from additional enterprise notes into concrete extension points
- Missing cross-repo single-source-of-truth inventory
- Missing mandatory component-first and Playwright-first UI validation rules at the subbundle level
- Missing process-definition MCP projection, install wiring, and restart-ready Codex configuration
