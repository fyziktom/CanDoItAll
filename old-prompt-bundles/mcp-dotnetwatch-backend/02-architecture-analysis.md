# Architecture Analysis

## Current state

The current server keeps live state inside the stdio MCP process:

- `src/CanDoItAll.Mcp.DotNetWatch/Runtime/AppRuntimeModels.cs`
  - `AppRuntimeManager` owns live app sessions in memory.
- `src/CanDoItAll.Mcp.DotNetWatch/Runtime/SessionCoordinator.cs`
  - all tool behavior is executed in-process.
- `src/CanDoItAll.Mcp.LocalRuntime/Processes/ProcessSupervisor.cs`
  - child processes are started by the MCP server process.

This means:

1. If the MCP server process exits, the control plane is lost even if child app processes are still alive.
2. A new MCP server instance starts with empty in-memory state.
3. Reuse across MCP server re-instancing is impossible without rediscovering and rehydrating runtime state.

## Why stale-process cleanup is not enough

The repo already has:

- `StaleProcessRegistry`
- `ServerInstanceRegistry`

These solve crash cleanup and ownership validation. They do not solve persistent control-plane ownership. They assume the current server instance is the owner of active runtime state.

## Target architecture

Use a two-process model:

1. Backend daemon/service process
   - long-lived
   - owns app sessions, operations, logs, process supervision, and state
   - hosts a lightweight local HTTP control API and a simple manager UI
2. MCP stdio proxy process
   - short-lived
   - ensures the backend daemon exists
   - connects to the backend
   - exposes MCP tools by proxying requests to the backend API

## Why HTTP/minimal API is the right control plane here

1. It is easy to debug manually.
2. It supports a manager UI with almost no extra infrastructure.
3. It allows clean reconnect from fresh MCP stdio instances.
4. The existing `CanDoItAll.Manager` is already a usable template for this pattern.

## Required backend identity model

The backend must be keyed by a workspace/settings identity, not only by process existence.

Minimum identity inputs:

1. workspace root
2. settings file path
3. settings content hash
4. server name

If any of those change materially, the stdio proxy must not silently connect to the wrong daemon.

## Required daemon bootstrap behavior

The stdio server must:

1. read a persisted backend registration file
2. verify the registered process is alive and matches startup time
3. verify the backend responds to a health/ping endpoint
4. verify identity compatibility
5. reuse it if valid
6. otherwise acquire a startup lock and spawn exactly one new backend

## Required backend registration data

Persist at least:

1. PID
2. process start time
3. base URL
4. auth token/shared secret
5. workspace root
6. settings path
7. settings hash
8. version marker
9. started-at timestamp
10. bound port or base URL source

## App session model changes

The current `AppRuntimeManager` only supports one active session. The backend must support many live sessions.

Minimum model change:

1. store sessions in a live-session dictionary
2. track a default/last-used session for backward-compatible no-argument status calls
3. reuse by full compatibility match
4. detect conflicts by at least:
   - same project path
   - overlapping requested URLs
   - same working directory plus incompatible launch shape

## Operation model changes

Build/test operations currently reason about a single active session. The backend must reason about a set of live sessions.

Required behavior:

1. identify which sessions conflict with the requested operation
2. leave unrelated sessions running
3. stop/resume only conflicting sessions when policy requires it
4. expose which sessions were preempted and resumed

## Agent-misuse hardening

The system must assume agents ignore instructions sometimes.

Required hardening:

1. `app_start` must aggressively reuse compatible live sessions.
2. stop semantics must be explicit and visible in tool descriptions.
3. backend reconnect must be automatic.
4. manager UI must expose enough state to see duplicate-session mistakes quickly.
5. status payloads must expose stable backend-owned session IDs, watcher PIDs, runtime PIDs, and launch identity.
6. backend startup must not leak daemon logs into MCP stdio output.

## Concerns the user did not explicitly name but must be handled

1. Backend auth:
   - local-only loopback bind is not enough; use a shared secret header/token.
2. Version skew:
   - a newer stdio proxy must reject or replace an incompatible older daemon.
3. Startup races:
   - multiple agents can start multiple stdio proxies at once.
4. Stale registrations:
   - registry files and daemon records must be validated, not trusted.
5. Log continuity:
   - logs and cursors must remain stable across stdio proxy re-instancing.
6. Session discovery:
   - no-argument status calls need a deterministic default live session.
7. Graceful shutdown:
   - there should be a backend shutdown path for maintenance, even if it is not the default workflow.
8. Multi-session observability:
   - the manager UI and `workspace_info` should show more than one live app session.
9. Backward-compatible MCP surface:
   - existing tool names should stay stable; payloads can be extended.
10. Validation realism:
   - use real MCP process re-instancing and a live browser, not just in-memory unit tests.
11. Detached daemon startup:
   - the backend process must not inherit MCP stdio handles in a way that keeps the daemon tied to proxy lifetime.
12. Port allocation:
   - daemon startup must handle port collisions deterministically and record the selected endpoint.
13. Health-disabled generic apps:
   - when a workspace does not expose a custom runtime health endpoint, watch sessions still need a stable settled signal based on observed listening URLs and explicit `dotnet watch` success lines such as static-asset hot reload completion.
14. Workspace isolation:
   - “generic for any C# app” should mean “same binary, different workspace-local settings identity”, not “one workspace boundary that can launch arbitrary external paths”.

## Recommended implementation direction

Use the same executable in two modes:

1. `stdio` mode: MCP proxy
2. `backend` mode: local web daemon

Reason:

1. It avoids duplicating domain model assemblies.
2. The backend can reuse the current runtime/process services.
3. Deployment remains simple.
4. Validation through the existing integration harness is straightforward.
