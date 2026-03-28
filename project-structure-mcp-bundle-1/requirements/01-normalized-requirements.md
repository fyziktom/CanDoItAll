# Normalized Requirements

| Requirement | Description | Source |
| --- | --- | --- |
| `R001` | Add a new project-structure MCP server that exposes typed tools for project, subproject, node, checklist, knowledge, asset, and lease operations. | `raw request` |
| `R002` | The MCP server must run locally on each Codex workstation but connect to the main CanDoItAll machine over HTTP instead of direct DB or file access. | `raw request` |
| `R003` | Keep the main data authority in CanDoItAll web and reuse existing `ProjectsService` and `ProjectWorkbenchService` instead of introducing a parallel project-structure persistence model. | `raw request`, `current-state analysis` |
| `R004` | Add central CanDoItAll web endpoints for filtered project-structure read access, project and node mutations, asset retrieval, knowledge guidance, and lease management. | `raw request` |
| `R005` | Add central agent policy settings inside CanDoItAll web so an agent can be enabled or disabled and limited by allowed capabilities and approval thresholds. | `raw request` |
| `R006` | Approval rules must be enforced centrally, including estimate-based approval thresholds and project-specific overrides when configured. | `raw request` |
| `R007` | Add a central lease or reservation mechanism so independent MCP instances on different computers cannot mutate the same project scope or repo-branch scope simultaneously. | `raw request` |
| `R008` | Lock conflicts must return actionable details about who owns the lease and what scope is busy. | `raw request` |
| `R009` | The read surface must support context reduction by filtering nodes and omitting layout/helper fields unless explicitly requested. | `raw request` |
| `R010` | Add a checklist query that returns unfinished items, prerequisite context, and effective priority with child-to-parent propagation unless the relevant ancestor is paused, stopped, or complete. | `raw request` |
| `R011` | Expose project-management guidance through a dedicated API or tool surface, backed by a swappable knowledge-provider abstraction and seeded with static best-practice guidance now. | `raw request` |
| `R012` | The guidance surface must include the explicit mission statement about making surroundings better and making projects successful. | `raw request` |
| `R013` | Assets must remain readonly when retrieved through the MCP. If an asset changes, the new version must be created as a new asset node under the original asset node, not overwritten in place. | `raw request` |
| `R014` | The new MCP must support project and structure management flows including project creation, subproject creation, node creation, node updates, plan/report support, and approval-request recording. | `raw request` |
| `R015` | The initiative must include an import entry point for externally described project structures and be architected for additional formats even if the first pass keeps format handling focused. | `raw request` |
| `R016` | The implementation must isolate reusable shared parts that can help other MCPs later and cover those parts with tests first when they become a dependency. | `raw request` |
| `R017` | Add cross-machine setup and reinstall material, preferably surfaced in the web UI and backed by scripts, README guidance, and MCP config output. | `raw request` |
| `R018` | Add automated coverage for service, API, and MCP-client behavior, including policy enforcement, locking, checklist logic, and asset handling. | `validation expectation` |
| `R019` | Add real end-to-end validation that creates and reads back a delivery block plus document assets, including Excel and PDF examples, through the new MCP path. | `raw request` |
| `R020` | Record validation analytics and final closure evidence so post-implementation validation can compare expected versus shipped behavior. | `raw request` |
