# Implementation Plan

## Phase 1: Establish the persistent backend shape

### Deliverables

1. Dual-mode startup in `CanDoItAll.Mcp.DotNetWatch`
2. backend registry/bootstrap logic
3. backend HTTP control API with health endpoint
4. stdio MCP proxy client
5. detached backend process launch that keeps MCP stdio clean

### File targets

1. `src/CanDoItAll.Mcp.DotNetWatch/Program.cs`
2. `src/CanDoItAll.Mcp.DotNetWatch/Configuration/McpServerOptions.cs`
3. `src/CanDoItAll.Mcp.DotNetWatch/Configuration/RuntimeConfiguration.cs`
4. new backend files under:
   - `src/CanDoItAll.Mcp.DotNetWatch/Backend/`
   - `src/CanDoItAll.Mcp.DotNetWatch/Manager/`

### Acceptance criteria

1. Starting the stdio server spawns the backend only once.
2. Starting a second stdio server reuses the backend.
3. Killing the stdio server does not kill the backend or the managed app.
4. Backend logs do not corrupt MCP stdio transport.

## Phase 2: Move runtime ownership into the backend

### Deliverables

1. `SessionCoordinator` hosted only in backend mode
2. proxy transport from MCP tools to backend API
3. durable backend-owned session and operation state

### File targets

1. `src/CanDoItAll.Mcp.DotNetWatch/Runtime/SessionCoordinator.cs`
2. `src/CanDoItAll.Mcp.DotNetWatch/Tools/CanDoItAllTools.cs`
3. new backend client/service files

### Acceptance criteria

1. MCP tools no longer depend on in-process runtime state.
2. A fresh stdio proxy can read status/logs for sessions started by an older stdio proxy.

## Phase 3: Add multi-session app management

### Deliverables

1. multiple live app sessions
2. compatibility reuse across sessions
3. conflict detection and replace behavior per launch shape
4. workspace info that can surface multiple live sessions

### File targets

1. `src/CanDoItAll.Mcp.DotNetWatch/Runtime/AppRuntimeModels.cs`
2. `src/CanDoItAll.Mcp.DotNetWatch/Contracts/ToolContracts.cs`
3. `src/CanDoItAll.Mcp.DotNetWatch/Runtime/SessionCoordinator.cs`

### Acceptance criteria

1. Two different projects can be watched concurrently when ports/settings do not conflict.
2. Starting the same project twice with the same template reuses the same session.
3. Starting the same project with incompatible launch data either fails or replaces only conflicting sessions.

## Phase 4: Harden stop/preemption behavior

### Deliverables

1. explicit stop bias
2. clearer tool descriptions
3. operation preemption that only affects conflicting sessions
4. richer preemption/resume payloads

### File targets

1. `src/CanDoItAll.Mcp.DotNetWatch/Tools/CanDoItAllTools.cs`
2. `src/CanDoItAll.Mcp.DotNetWatch/Contracts/ToolContracts.cs`
3. `src/CanDoItAll.Mcp.DotNetWatch/Runtime/SessionCoordinator.cs`
4. `CanDoItAll.Mcp.DotNetWatch.settings.json`

### Acceptance criteria

1. The happy path is reuse, not stop.
2. Stop is visible, explicit, and not implied by MCP re-instancing.
3. Operation metadata states exactly which sessions were stopped and resumed.

## Phase 5: Add backend manager UI

### Deliverables

1. simple daemon dashboard
2. app session list
3. operation list
4. backend identity/status panel
5. links/log views

### File targets

1. new manager UI files under `src/CanDoItAll.Mcp.DotNetWatch/Manager/`
2. optional shared styling adapted from `tools/CanDoItAll.Manager`

### Acceptance criteria

1. Backend URL is reachable locally.
2. A human can confirm which sessions exist, which one is reused, and which PIDs are active.

## Phase 6: Cover with tests

### Deliverables

1. backend bootstrap tests
2. re-instancing integration test
3. multi-session reuse/conflict tests
4. log/status continuity tests

### File targets

1. `tests/CanDoItAll.Mcp.DotNetWatch.Tests/`
2. `tests/CanDoItAll.Mcp.DotNetWatch.IntegrationTests/`

### Required new scenarios

1. start app via harness A, dispose harness A, query status via harness B, same session still live
2. repeated stdio startups do not spawn multiple backends
3. compatible `app_start` across harnesses reuses one backend-owned session
4. backend stale registry is replaced safely

## Phase 7: Live validation edit loop

### Deliverables

1. project structure page layout fix
2. live watch confirmation through backend reuse
3. browser proof

### File targets

1. `src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.razor`
2. `src/CanDoItAll.ComponentKit/wwwroot/canvas-workbench.css`
3. any directly related component files only if needed

### Acceptance criteria

1. The lower section no longer stretches endlessly with large lists.
2. The lower section cards/columns remain readable.
3. Re-instancing the MCP server does not drop the live app.
4. A styling edit after re-instancing is visible in the browser without rebuilding the whole control plane.
