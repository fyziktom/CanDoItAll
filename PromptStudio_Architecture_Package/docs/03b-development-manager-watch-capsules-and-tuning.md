# 03B - Development Manager, Watch Loop, Capsules, and Interactive Tuning

## 1. Purpose

This document defines the development-acceleration subsystem that closes the loop between:

1. source code changes
2. `dotnet watch`
3. machine-readable readiness signals
4. Codex-driven edits
5. Playwright verification
6. always-fresh compressed documentation about the real source state

This is not optional tooling. It is a deliberate part of how the application should be built quickly and safely.

## 2. Scope and naming

The recommended concrete implementation is a separate local tool project named:

- `CanDoItAll.Manager`

Within this architecture package, it is referred to as the **development manager** or **manager sidecar**.

It is a local-only development tool that:

- supervises `dotnet watch` for the main app
- exposes a loopback-only OpenAPI and event stream for Codex
- generates Codex-optimized capsule artifacts from source comments
- accepts targeted tuning requests from the running UI
- correlates Codex job completion with watch readiness and optional verification

It is not a replacement for the application modules. It is a productivity and coordination layer around them.

## 3. Why this subsystem matters

Without a formal manager:

- Codex has no trustworthy way to know when a change is actually live
- Playwright runs can start too early
- console output remains human-readable but not machine-usable
- source documentation drifts away from implementation
- targeted UI tuning becomes slow, manual, and error-prone

With the manager in place, the team gets a repeatable local loop:

1. make or request a change
2. wait for a reliable ready signal
3. run browser verification
4. review the outcome
5. keep the compressed source map current automatically

## 4. Core goals

The development manager must:

- turn `dotnet watch` state into a machine-readable contract
- keep recent watch output available for diagnostics
- confirm readiness using both watch signals and an app readiness probe
- generate short but useful source capsules for Codex from in-file comments
- show missing, invalid, or stale capsules as explicit drift
- support dev-only tuning requests from a specific UI component
- correlate a tuning request, Codex job, watch iteration, and verification result
- notify the running UI only when the requested change is genuinely ready for review

## 5. Architectural stance

### 5.1 Separation of concerns

Use a separate local ASP.NET Core application for the manager. Do not bury this logic inside the main Blazor app.

Reason:

- process supervision is easier to isolate
- OpenAPI and event streaming become straightforward
- failures in the dev loop do not pollute the main domain model
- the tool can be started, stopped, and versioned independently

### 5.2 Hosting model

The manager should be implemented as:

- ASP.NET Core Minimal API
- hosted background services for watch supervision and capsule generation
- loopback-only endpoints
- development-environment-only startup profile

### 5.3 Security boundary

The manager is still local software, so it needs real boundaries:

- bind only to `127.0.0.1` or `localhost`
- use an ephemeral session token for mutating or sensitive endpoints
- allow only approved workspace roots
- never expose raw secrets, prompt payloads, or arbitrary file contents by default
- keep tuning mode disabled unless explicitly enabled

## 6. Manager responsibilities

The manager owns five responsibilities:

1. **Watch supervision**  
   launch, observe, restart, and normalize `dotnet watch`

2. **Readiness signaling**  
   provide a trustworthy `ready` contract for Codex and Playwright

3. **Capsule generation**  
   watch source changes and generate Codex-optimized capsule artifacts

4. **Tuning orchestration**  
   accept targeted tuning requests and track them through completion

5. **History and diagnostics**  
   retain recent logs, state transitions, failures, and change correlation

## 7. `dotnet watch` supervision design

### 7.1 Launch contract

The manager should supervise the main app using the official command-line watcher:

```bash
dotnet watch --project src/PromptStudio.Web run --non-interactive
```

This matches current Microsoft guidance for `dotnet watch` on .NET 10 and avoids interactive prompts that would block automation.

If the repository wants stricter artifact isolation, the manager may also use a dedicated `--artifacts-path` under an excluded local artifacts root.

### 7.2 Recommended environment variables

Use these defaults for manager-supervised sessions:

- `DOTNET_WATCH_SUPPRESS_EMOJIS=1`
- `DOTNET_WATCH=1` is already set by `dotnet watch` for child processes
- `DOTNET_WATCH_SUPPRESS_BROWSER_REFRESH=1` in controlled-agent mode
- `DOTNET_USE_POLLING_FILE_WATCHER=1` only for file systems that need polling

Notes:

- suppressing emojis keeps console parsing stable across terminals
- controlled-agent mode should prefer explicit readiness and explicit browser actions over implicit refresh behavior
- polling should remain opt-in because it costs more than normal file watching

### 7.3 Main app readiness probe

Do not treat one console line as sufficient proof that the app is ready.

The main web app should expose a development-only readiness endpoint such as:

- `GET /_dev/runtime`

Recommended payload:

- current environment
- current `DOTNET_WATCH_ITERATION`
- active application base URL list
- process start time
- app version or build stamp
- readiness boolean
- last startup exception summary if startup failed

The manager should mark a change as fully ready only after:

1. watch output indicates a successful build or hot reload cycle
2. the readiness endpoint responds successfully
3. the returned iteration is equal to or newer than the expected iteration

### 7.4 Watch state machine

Normalize raw watch behavior into explicit states:

- `Idle`
- `Starting`
- `Building`
- `Launching`
- `Ready`
- `HotReloadApplied`
- `Restarting`
- `BuildFailed`
- `RuntimeFaulted`
- `Stopped`

The manager may keep more internal detail, but the external contract should stay stable.

### 7.5 Structured watch events

Each state transition should become a structured event with:

- event id
- correlation id
- state
- timestamp
- expected watch iteration
- confirmed watch iteration if known
- short summary
- raw line reference into the log buffer

### 7.6 Log history

Retain a rolling history of:

- raw output lines
- normalized events
- last successful ready event
- last failed build or runtime fault

This history should be queryable by timestamp or count.

### 7.7 Failure handling

The manager must handle at least these cases explicitly:

- compile failure
- runtime crash after build success
- readiness probe timeout
- watch process exit
- repeated restart loop
- file-watch storm

One failure must not permanently poison the manager. Recovery should be visible and restartable.

## 8. Local OpenAPI and event contract

### 8.1 API style

Use:

- `AddOpenApi`
- `MapOpenApi`
- Minimal APIs

Expose the OpenAPI document only on the manager itself and keep it local-only.

### 8.2 Recommended endpoints

The manager should expose at least:

- `GET /api/watch/status`  
  current normalized watch state and active application URLs

- `GET /api/watch/logs?take=200`  
  recent raw or normalized output

- `GET /api/watch/wait-ready?afterEventId=123&timeoutMs=90000`  
  long-poll style readiness wait

- `GET /api/watch/events`  
  SSE stream for watch and manager events

- `GET /api/capsules/index`  
  current capsule index summary

- `GET /api/capsules/symbols/{symbolId}`  
  one capsule document

- `GET /api/capsules/coverage`  
  coverage and drift report

- `GET /api/capsules/changed?sinceUtc=...`  
  incremental capsule changes

- `POST /api/tuning/requests`  
  create a tuning request

- `GET /api/tuning/requests/{requestId}`  
  status and summary

- `GET /api/tuning/requests/{requestId}/events`  
  SSE stream for tuning job progress

- `POST /api/tuning/requests/{requestId}/cancel`  
  cancel a pending or running request

### 8.3 SSE over polling

Polling is acceptable as a fallback, but SSE should be the preferred contract for:

- watch state changes
- capsule refresh completion
- tuning request lifecycle updates
- ready-for-review notifications

ASP.NET Core 10 now supports server-sent events cleanly in Minimal APIs, which fits this manager very well.

### 8.4 API authentication and safety

The manager is local-only, but mutating endpoints should still require:

- a per-session token header such as `X-CanDoItAll-Manager-Session`
- a valid allowed workspace root
- request-size limits
- secret-redaction on all returned diagnostics

## 9. Codex capsules

### 9.1 Capsule purpose

The source tree needs a compressed, always-nearby description of what each important component or type currently does.

This is called a **Codex capsule**.

Capsules exist to:

- keep Codex aligned to the real source state
- reduce repeated codebase rediscovery
- help generate short, high-value agent context
- support runtime tuning mode with a meaningful summary

### 9.2 Coverage rule

Capsules are required for:

- every handwritten `.razor` component
- every page component
- every significant C# class, record, struct, enum, and interface
- every service that owns state, orchestration, validation, or external integration

Allowed exemptions:

- generated code
- EF migrations
- trivial model files with no real behavior
- third-party or copied vendor code

Exemptions must use an explicit skip marker so missing coverage is intentional, not accidental.

### 9.3 Capsule format

Use a short, structured comment block directly above the declaration.

Required fields:

- `kind`
- `name`
- `summary`
- `owns`
- `deps`
- `risks`
- `tests`

Recommended optional fields:

- `inputs`
- `outputs`
- `state`
- `restore`
- `tuning`

Field rules:

- one logical line per field
- keep summaries concise
- use comma-separated tokens or short phrases, not paragraphs
- do not include secrets, credentials, or raw prompt payloads

### 9.4 Example for C#

```csharp
/* codex-capsule
kind: service
name: TabHostService
summary: Opens, restores, sleeps, and rehydrates internal application tabs.
owns: tab-session, active-tab, restore-report
deps: ITabRegistry, ITabPersistenceStore, IClock
risks: duplicate-tab-key, stale-snapshot, dirty-state-loss
tests: unit:TabHostServiceTests, integration:WorkbenchRestoreTests
inputs: OpenTabRequest, TabSnapshot
outputs: TabOpened, TabSlept, TabWoken
state: ordered-tabs, active-tab-id, pinned-tabs
restore: local-storage snapshot versioned by schema
tuning: anchor=tab-host; inspect=active-tab, tab-kind
*/
public sealed class TabHostService : ITabHostService
{
}
```

### 9.5 Example for Razor

```razor
@* codex-capsule
kind: component
name: ProjectStructureCanvas
summary: Hosts the project structure canvas wrapper and coordinates selection, outline, and inspector state.
owns: selected-node, viewport, outline-sync
deps: IProjectStructureFacade, ITabHostService
risks: stale-selection, invalid-link-target, heavy-viewport-state
tests: component:ProjectStructureCanvasTests, e2e:workbench/project-structure.spec.ts
inputs: ProjectId, ManifestId
outputs: OnNodeSelected, OpenArtifactRequest
state: selected-node-id, viewport-state, inspector-tab
restore: viewport, selection, inspector-tab
tuning: anchor=canvas-shell; inspect=selected-node, node-kind
*@
```

### 9.6 Skip marker example

Use a compact explicit skip marker for exempt files or declarations, for example:

```csharp
/* codex-capsule-skip
reason: generated migration
*/
```

### 9.7 Capsule generation outputs

Generate artifacts under a manager-controlled path such as:

```text
.artifacts/codex-capsules/
  index.json
  index.md
  coverage.json
  modules/
  symbols/
  changed/
```

Artifacts must be:

- incremental
- machine-readable first
- human-readable second
- excluded from app rebuild loops unless intentionally consumed

### 9.8 Capsule drift rules

The manager should flag:

- missing required capsules
- malformed capsules
- symbol names that no longer match the declaration
- source changes that occurred after the last capsule generation
- stale test references or missing test classification markers when detectable

Capsule drift should be visible in the manager API and in the app's dev-only tuning UI.

## 10. Tuning mode

### 10.1 Purpose

Tuning mode is a dev-only UX that lets a user target a specific component or page fragment from the running app and create a high-context change request quickly.

### 10.2 UX model

In tuning mode:

- each tunable component exposes a small corner handle
- clicking the handle opens a tuning panel
- the user can paste or attach an image from the clipboard
- the panel preloads the component capsule
- the panel includes route, project, tab, and selection context
- the user adds a short free-form instruction
- the request is then sent to the manager

This should be hidden entirely outside development mode.

### 10.3 Runtime metadata required from the UI

Tunable components should publish stable metadata such as:

- capsule key
- component name
- route
- current project id if available
- internal tab id if available
- selected entity id if available

This metadata should be emitted through a shared `TunableComponentBoundary` pattern, not reimplemented ad hoc per page.

### 10.4 Tuning request payload

A tuning request should include:

- request id
- correlation id
- workspace root
- app route
- project id
- tab id
- capsule key and capsule snapshot
- optional pasted image or screenshot attachment
- user instruction
- requested validation target if any
- created timestamp

### 10.5 Codex CLI execution policy

The manager may invoke Codex CLI only when:

- tuning mode is enabled explicitly
- the workspace root is approved
- the request was created locally by the user
- the request packet has been redacted and validated

The manager should support two modes:

- `ReviewBeforeSend`
- `AutoSendForDev`

Default to `ReviewBeforeSend`.

### 10.6 Tuning job lifecycle

Track at least these statuses:

- `Queued`
- `Packaging`
- `AwaitingApproval`
- `SubmittedToCodex`
- `CodexRunning`
- `ChangesApplied`
- `WaitingForWatchReady`
- `ReadyForReview`
- `VerificationPassed`
- `VerificationFailed`
- `Failed`
- `Cancelled`

### 10.7 Ready-for-review notification

The UI should show the change as ready only when:

1. the Codex job has completed or yielded control
2. the watched app is ready again
3. any requested verification is finished or explicitly skipped
4. changed files do not introduce unreported capsule drift

This prevents false-positive "done" notifications while the app is still rebuilding or faulted.

### 10.8 Playwright coordination

The manager should not replace Codex's Playwright usage.

Instead, it should provide:

- waitable readiness
- current app URL and runtime state
- correlation ids
- optional place to record verification summaries

Codex remains free to use Playwright MCP directly after the manager signals readiness.

## 11. Important controls that were missing from the original idea

The following details are necessary for this feature to be operationally safe and useful:

### 11.1 Correlation everywhere

Every tuning request should correlate:

- tuning request id
- Codex job id
- watch event id
- watch iteration
- verification result id

Without this, history becomes ambiguous quickly.

### 11.2 Readiness must not rely on console parsing alone

Console parsing is useful, but it is not enough. The manager needs a development-only runtime probe from the main app.

### 11.3 Generated artifacts must not trigger infinite loops

Capsule outputs, tuning screenshots, and manager logs must live in excluded paths or they will create rebuild storms.

### 11.4 Missing capsules must be visible early

If capsule adoption is optional or silent, it will decay immediately. Coverage reporting and drift warnings are mandatory.

### 11.5 Tuning mode must be visibly dev-only

The product should never accidentally expose Codex job submission controls in a normal production session.

### 11.6 Secrets must be redacted from requests and logs

Capsules, screenshots, and tuning request summaries can accidentally expose sensitive data if not filtered.

### 11.7 Fake adapters are required for tests

The manager needs test doubles for:

- watch process output
- readiness probe responses
- Codex CLI execution
- capsule file scanning

Otherwise its automation layer will be hard to test reliably.

### 11.8 Debounce and back-pressure are required

Rapid file changes must not create duplicated capsule generation or misleading watch state churn.

## 12. Recommended implementation boundary for the first version

The first serious version should include:

- one supervised watch session for the main web app
- normalized watch states and history
- loopback OpenAPI and SSE
- development-only runtime readiness endpoint in the main app
- required capsules for all touched foundation files
- incremental capsule generation artifacts
- dev-only tuning overlay and request creation
- tracked tuning requests with ready-for-review notification

The first version does not need:

- multi-workspace scheduling
- remote Codex agents
- production deployment of the manager
- automatic multi-app orchestration

## 13. QA gates for this subsystem

This subsystem is acceptable only if:

- the manager survives app build failures and recovers cleanly
- a false `ready` state is not emitted during build failure or runtime crash
- capsule coverage is measurable and drift is visible
- tuning mode is hidden outside development mode
- a tuning request can be traced from UI trigger to watch-ready result
- generated artifacts do not create self-triggering watch loops
- Playwright can wait on the manager instead of using arbitrary sleeps

## 14. Final conclusion

This manager-and-capsule subsystem is one of the highest leverage additions to the architecture.

It speeds delivery because it replaces guesswork with explicit signals, keeps agent context fresh, and turns targeted UI refinement into a short controlled loop instead of a manual rebuild-and-describe cycle.
