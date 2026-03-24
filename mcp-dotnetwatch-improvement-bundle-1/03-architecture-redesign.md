# 03. Architecture Redesign

## Architectural stance

Do not replace the current backend architecture.
Refactor it into a clearer control plane plus execution lanes.

The current design already proved that the detached backend is the right authority for runtime state.
The redesign should therefore strengthen boundaries instead of flattening them.

## Current-to-target component map

### Preserve and harden

- `BackendConnectionManager`
- `BackendToolInvoker`
- `AppRuntimeManager`
- `SessionCoordinator`
- `ResourceMutationGate`
- wrapper bootstrap script
- manager UI and backend catalog

### Introduce or split

- `BridgeRepairCoordinator`
- `AppLaunchSpec` model hierarchy
- `RuntimeLaneCoordinator`
- `RuntimeSlotRegistry`
- `AtomicUpdateCoordinator`
- `RuntimeRevisionService`
- `SessionEventJournal`
- `ResourceScopePlanner`
- `ShadowBuildRetentionService`

## Control-plane redesign

### 1. Bridge repair coordinator

Problem:

- today the stdio host can cache a dead or stale backend connection and surface generic failures

Target behavior:

1. every tool invocation uses a connection acquisition policy, not a one-time startup assumption
2. on first HTTP/connect/auth failure, the bridge attempts:
   - re-read registration
   - re-ping candidate backend
   - re-launch backend if needed
3. idempotent requests are retried once automatically
4. non-idempotent requests are retried only if the backend did not acknowledge receipt
5. the final error code must explain whether the failure is:
   - backend missing
   - backend auth mismatch
   - backend health probe failed
   - bridge could not repair

Proposed implementation slice:

- keep `BackendConnectionManager`
- add a `TryRepairAsync` path
- make `BackendToolInvoker` route every call through a `SendWithRepairAsync` flow

### 2. Bridge diagnostics as first-class data

`workspace_info` should include a bridge subsection with:

- current bridge mode
- current backend id
- last successful backend ping
- last repair attempt
- current shadow build signature
- current shadow manifest path

This reduces guesswork during Codex sessions.

## Launch model redesign

### Problem

`AppRunMode` conflates the source of the runtime with how it is launched.

### Target

Separate:

- what is being launched
- how it is being launched
- which lane owns it

Suggested model:

```csharp
public enum AppLaunchType
{
    Project,
    PublishedDll,
    Executable
}

public enum RuntimeLaneKind
{
    SourceWatch,
    SourceRun,
    PublishedCandidate,
    PublishedActive,
    ExternalExecutable
}
```

`AppLaunchSpec` should become a discriminated model:

- `ProjectLaunchSpec`
- `PublishedDllLaunchSpec`
- `ExecutableLaunchSpec`

`AppStartTemplate` should be retired or wrapped by the new model rather than overloaded further.

## Runtime lane coordinator

Introduce a coordinator that owns policy decisions across lanes:

- source-watch reuse
- safe preemption for build/test
- candidate slot preparation
- commit and rollback
- lane-specific wait semantics

This coordinator should sit above the existing app runtime manager and operation registry.

It should not own process launching details directly.

## Atomic runtime redesign

### 1. Slot registry

Replace the single publish output folder with a registry under:

`.mcp-state/runtime-slots/<app-key>/`

Suggested layout:

```text
.mcp-state/runtime-slots/<app-key>/
  active.json
  history/
  slot-a/
    manifest.json
    payload/
  slot-b/
    manifest.json
    payload/
  transactions/
    <transaction-id>.json
```

### 2. Candidate preparation

Candidate preparation must:

1. choose the inactive slot
2. publish into that slot only
3. generate a slot manifest containing:
   - logical app id
   - source signature
   - publish hash
   - build configuration
   - produced entry path
   - candidate health URLs
   - creation timestamp

### 3. Candidate launch

Candidate runtime must launch on isolated ports, not active runtime ports.

Reason:

- health validation must not require stopping the current active runtime
- running published output must never lock the active slot for the next prepare

### 3a. Endpoint allocation

Candidate ports cannot be improvised.
Introduce a small endpoint allocator, for example:

- `RuntimeEndpointAllocator`
- `PortLeaseRegistry`

Responsibilities:

1. allocate loopback-only port pairs for candidate sessions
2. persist the lease while the candidate is alive
3. prevent collisions across active watch, build-test helpers, and published candidates
4. release the lease on stop, rollback, or failed prepare cleanup

This should be configuration-driven, not hard-coded.

### 4. Commit

Commit changes the logical active runtime pointer after candidate health succeeds.

Bundle 1 commit model:

1. candidate is already running and healthy on isolated ports
2. the backend marks the candidate revision as active for the logical app id
3. the old active session becomes previous/rollbackable
4. MCP responses now resolve the logical app id to the new active session and URLs

This is atomic for Codex because the logical active runtime changes only after validation.

### 5. Rollback

Rollback means:

1. resolve previous active slot/session from transaction history
2. restore logical active pointer
3. return structured rollback evidence

## Resource coordination redesign

### Problem

The current global workspace lock prevents some races but destroys lane-level concurrency and does not model slot isolation.

### Target resource graph

Use named resources such as:

- `bridge`
- `backend-registration`
- `shadow-build`
- `source-tree:<project>`
- `logical-app:<logicalAppId>`
- `slot:<logicalAppId>:a`
- `slot:<logicalAppId>:b`
- `operation:<operationId>`

### Rules

1. source-watch mutation conflicts with build/test against the same source tree when those operations are not declared safe
2. publish prepare locks only the inactive slot plus the relevant source tree
3. commit locks the logical app id and the candidate slot, not the whole workspace
4. shadow build refresh must not compete with runtime slots

## Structured event journal

Current log reduction is good, but Codex still has to infer lifecycle from logs more than necessary.

Add a structured journal for:

- session created
- session reused
- source change detected
- revision confirmed
- candidate prepared
- candidate healthy
- transaction committed
- rollback committed
- bridge repaired

This should be queryable incrementally and should coexist with raw logs.

## Shadow-host governance

The wrapper already creates immutable build roots and a current manifest.
Bundle 1 should add:

1. retention rules
2. in-use detection before cleanup
3. safe deletion of old builds
4. manifest validation at process start

Retain at minimum:

- current build
- previous build
- newest failed build evidence

## Self-host validation isolation

Bundle 1 must make the following rule explicit in code and validation:

- the live backend and its tests/builds must never depend on mutating the same loaded output directory

Recommended direction:

1. continue using shadow builds for the live stdio host and detached backend
2. route MCP server build/test validation through dedicated artifacts roots
3. keep that behavior available through normal managed build/test tools so Codex does not need manual shell workarounds

This is part of fluency.
If the server cannot validate itself while live, Codex will still fall back to brittle manual recovery loops.

## Configuration additions

Suggested new settings groups:

- `Bridge`
  - repair retry policy
  - ping timeout
- `AtomicRuntime`
  - slot root
  - rollback retention
  - default candidate configuration
- `Endpoints`
  - candidate port ranges
  - lease persistence path
- `ShadowHost`
  - retained build count
  - cleanup policy

Bundle 1 should keep defaults conservative and keep current settings valid.

## Suggested code reorganization

Likely file areas:

- `src/CanDoItAll.Mcp.DotNetWatch/Bridge/`
  - `BridgeRepairCoordinator.cs`
  - `BridgeStatusModels.cs`
- `src/CanDoItAll.Mcp.DotNetWatch/Runtime/LaunchSpecs/`
  - `AppLaunchSpec.cs`
  - `ProjectLaunchSpec.cs`
  - `PublishedDllLaunchSpec.cs`
  - `ExecutableLaunchSpec.cs`
- `src/CanDoItAll.Mcp.DotNetWatch/Runtime/Lanes/`
  - `RuntimeLaneCoordinator.cs`
- `src/CanDoItAll.Mcp.DotNetWatch/Runtime/Atomic/`
  - `RuntimeSlotRegistry.cs`
  - `AtomicUpdateCoordinator.cs`
  - `AtomicUpdateModels.cs`
- `src/CanDoItAll.Mcp.DotNetWatch/Runtime/Events/`
  - `SessionEventJournal.cs`
  - `SessionEventModels.cs`
- `src/CanDoItAll.Mcp.DotNetWatch/Runtime/Coordination/`
  - `ResourceScopePlanner.cs`

Do not force all of this into one giant rewrite.
Phased extraction is explicitly preferred.
