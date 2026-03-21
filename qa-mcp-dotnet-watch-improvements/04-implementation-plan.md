# Implementation Plan

## Goal

Make the DotNet Watch MCP server trustworthy for live editing, not just fast in the happy path.

## Phase 1: Fix Wait Correctness

### Deliverables

- watch-generation-aware wait model
- no stale `Healthy` reuse after file changes
- no early `QuietSinceCursor` success while a later watch stage is still pending

### Changes

- Update `src/CanDoItAll.Mcp.DotNetWatch/Runtime/AppRuntimeModels.cs`
  - recognize real watch messages
  - invalidate health and runtime generation when file-change processing begins
- Update `src/CanDoItAll.Mcp.DotNetWatch/Runtime/SessionCoordinator.cs`
  - stop treating old `Healthy` state as proof of current readiness
  - require generation completion plus fresh health confirmation
- Prefer generation-state transitions over plain "quiet log gap" heuristics

### Acceptance criteria

- `Healthy` never succeeds while build/restart output is still active
- `QuietSinceCursor` does not succeed until the active watch generation has settled
- `RestartCompleted` works on real Windows `dotnet watch` logs

## Phase 2: Surface The Right Runtime Identity

### Deliverables

- watcher PID and child runtime PID both visible
- expected vs confirmed watch generation visible
- clearer status/wait payloads for agents

### Changes

- Extend status models to expose:
  - watcher PID
  - active child PID
  - expected watch iteration
  - confirmed watch iteration
  - last hot reload outcome
- Consider renaming or documenting current PID fields to prevent misuse

### Acceptance criteria

- an agent can tell whether only the watcher exists or the child runtime is actually ready
- a status snapshot makes the current generation unambiguous

## Phase 3: Use `watchIteration` End To End

### Deliverables

- generation-aware health probing
- wait APIs tied to actual watch iteration confirmation

### Changes

- Preserve `WatchIteration` from `RuntimeProbePayload`
- Add it to outward-facing health/status payloads
- Let waits compare:
  - baseline generation seen before edit
  - confirmed generation after edit

### Acceptance criteria

- agents can wait for "next generation became healthy"
- hot reload and restart-needed flows both have deterministic confirmation

## Phase 4: Fix Self-Host Validation

### Deliverables

- live server can run its own tests safely
- integration test project participates in the solution consistently

### Changes

- isolate server execution outputs from test-build outputs
- add `tests/CanDoItAll.Mcp.DotNetWatch.IntegrationTests` to `CanDoItAll.slnx`
- update test expectations to cover the repaired lifecycle model

### Acceptance criteria

- `candoitall_tests_run` against the MCP test projects passes while the server is live
- solution-level tooling sees the same project inventory as workspace settings

## Phase 5: Agent-Facing Superpowers

### Deliverables

- one or more high-level synchronization affordances

### Candidate additions

- `app_wait_for_next_generation`
- `app_wait_for_watch_settle`
- richer `app_status` watch block:
  - `expectedWatchIteration`
  - `confirmedWatchIteration`
  - `runtimePid`
  - `watcherPid`
  - `watchState`

### Acceptance criteria

- an implementation agent no longer needs to infer propagation from raw logs
- browser refresh timing can be based on a strong contract instead of heuristics
