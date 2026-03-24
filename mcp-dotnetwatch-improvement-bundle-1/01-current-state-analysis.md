# 01. Current State Analysis

## Inputs reviewed

- `implementation-phase2/DOTNETWATCH_ANALYSIS.md`
- `CanDoItAll.Mcp.DotNetWatch.settings.json`
- `src/CanDoItAll.Mcp.DotNetWatch/Backend/BackendConnectionManager.cs`
- `src/CanDoItAll.Mcp.DotNetWatch/Backend/BackendToolInvoker.cs`
- `src/CanDoItAll.Mcp.DotNetWatch/Runtime/AppRuntimeModels.cs`
- `src/CanDoItAll.Mcp.DotNetWatch/Runtime/SessionCoordinator.cs`
- `src/CanDoItAll.Mcp.DotNetWatch/Runtime/WorkspaceExecutionLock.cs`
- `src/CanDoItAll.Mcp.DotNetWatch/Contracts/ToolContracts.cs`
- `tools/CanDoItAll.Mcp.DotNetWatch/Start-CanDoItAllDotNetWatchMcp.ps1`
- current unit and integration tests under `tests/CanDoItAll.Mcp.DotNetWatch.*`
- current bootstrap evidence in `.mcp-state/logs/mcp-dotnetwatch-bootstrap.log`

## Verified strengths

1. The detached backend design is working.
   Evidence from the analysis file and registration file shows the backend manager remains reachable and can own live app sessions independently of the MCP stdio host.

2. The wrapper-based shadow launch path exists and is already covered by tests.
   The repo already contains `Start-CanDoItAllDotNetWatchMcp.ps1` plus wrapper integration tests. Bundle 1 must build on that instead of re-planning from zero.

3. The watch lifecycle and health model are materially better than in earlier passes.
   Current tests already cover watch iteration tracking, restart-required flows, stale-health invalidation, wrapper launch, backend persistence, and log reduction.

4. Managed build/test operations already run through isolated artifacts paths.
   That is the right direction and should be preserved.

5. The app exposes useful runtime metadata through `/_dev/runtime`.
   In particular, `WatchIteration` already exists and should remain the authoritative live-source revision marker.

## Verified hard problems that still remain

### A. Direct Codex-to-MCP tool invocation is still not trustworthy

`DOTNETWATCH_ANALYSIS.md` is explicit:

- the detached backend is healthy
- the manager UI can control live sessions
- direct tool calls can still fail generically

That means the weakest link is now the bridge/control plane between Codex and the persistent backend, not the backend-owned runtime itself.

### B. The public runtime model is still too narrow

Current `AppRunMode` only supports:

- `WatchRun`
- `RunOnce`

There is no first-class runtime mode for:

- a published DLL
- a published apphost executable
- a prebuilt external executable
- an atomic candidate slot

That prevents the MCP server from turning the existing publish output into a managed runtime lane.

### C. The repo has two different runtime worlds that are not unified

Today there is a split:

- source-backed watch sessions that the backend can manage
- publish-backed validation output under `.artifacts\bundle-validation\webapp` that the backend cannot manage directly

This is the main structural blocker for atomic Codex-safe updates.

### D. The current mutation gate is too coarse for the next stage

`WorkspaceExecutionLock` serializes everything under a single `"workspace"` resource.

That is acceptable for simple correctness, but it is not a good long-term coordination model for:

- bridge repair
- source watch
- build/test operations
- slot-based publish preparation
- shadow-host refresh

Bundle 1 needs a resource graph, not one global mutex.

### E. Publish-backed validation still uses a lock-prone single target folder

The analysis file confirms that publish to `.artifacts\bundle-validation\webapp` succeeds only after stopping any running published host that locks the folder.

That is the opposite of an atomic update model.

### F. Codex has no explicit high-level contract for choosing fast vs atomic workflows

Current public tools are low-level enough that an agent still has to infer:

- when watch is appropriate
- when heavy build/publish work should avoid the live watch lane
- when a stable candidate runtime is needed
- when rollback should be available

That inference should move into the MCP contract itself.

### G. Bridge repair behavior is not strong enough for long-lived Codex sessions

`BackendToolInvoker` uses the connection cached at startup and does not implement a structured repair-and-retry flow after mid-session backend churn.

If the backend registration changes, the auth token rotates, or the current connection becomes stale, the tool path can degrade into a generic failure instead of a deterministic reconnect.

### H. Shadow build lifecycle still needs governance

The wrapper already writes immutable build roots and a `current.json` manifest, which is correct.
However, the bootstrap log still shows historical failure modes around locked shadow artifacts.
Bundle 1 must treat shadow-build cleanup, retention, and in-use safety as an explicit design area instead of assuming the wrapper alone is enough.

### I. Self-host validation isolation must remain explicit

This repo is not only using the MCP server.
It is also evolving the MCP server itself.

That means bundle 1 must continue to support this workflow safely:

- live backend remains running
- Codex changes `CanDoItAll.Mcp.DotNetWatch`
- focused tests/builds for the MCP server run through isolated artifacts

If that path is not explicitly designed, the project will regress into output-lock failures while trying to validate its own changes.

## What should not be re-planned

The following areas already have credible implementation or tests and should be preserved:

1. detached backend ownership of runtime state
2. wrapper-based stdio startup path
3. bootstrap logging to `.mcp-state/logs`
4. watch iteration-aware health confirmation
5. agent-optimized log reduction
6. operation artifact isolation
7. manager-backed session visibility and controls

## Working definition of "fluent work" for bundle 1

In this bundle, fluent work means:

- a small Razor, CSS, or safe hot-reloadable change should continue to use the source-watch lane
- the MCP server should expose enough structured revision state that Codex does not need to guess whether propagation finished
- heavy or riskier changes should be routable to a different lane without destabilizing the live source-watch lane

## Working definition of "atomic updates for Codex" for bundle 1

Atomicity in bundle 1 means:

1. the currently active runtime remains the authoritative runtime until candidate validation succeeds
2. a candidate runtime is prepared in isolation
3. commit switches the logical active runtime only after a health gate passes
4. failure to prepare or validate the candidate does not corrupt the current active runtime
5. rollback to the previous active runtime is supported

Non-goal for bundle 1:

- true zero-downtime preservation of the same public socket bindings without a relay/proxy

Bundle 1 is about Codex-safe runtime atomicity, not local blue-green networking perfection.
