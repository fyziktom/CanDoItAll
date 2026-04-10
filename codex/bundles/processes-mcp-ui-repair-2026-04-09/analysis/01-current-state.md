# Current State

- The `CanDoItAll.Mcp.Processes` server is reachable from this environment and can create, publish, and start process runs. The defect is not an MCP transport failure.
- The global `/processes` page in `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessWorkspace.razor.cs` skips its first data load when `ProjectId`, `processId`, and `runId` are all null because the cached parameter fields also start as null.
- The active UI database profile already contains process definitions. The empty global page is therefore a UI orchestration defect, not a missing-data or wrong-profile initialization defect.
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessesService.cs` currently computes role and step counts across every version of a definition. After publish creates a fresh draft clone, the list summary doubles counts instead of showing the authoritative version shape.
- The requested repair scope is intentionally narrow: fix UI loading, fix definition summary counting, keep database profile behavior intact, and avoid any token/settings work for Processes MCP.
