# Follow-Up Agent Plan

## Phase 1

Goal: restore plain-watch parity for nearby UI edits.

- change `SourceWatch` launch to stop using `--artifacts-path`
- keep build/test/atomic lanes unchanged for the first pass
- enable MSBuild server for `SourceWatch`
- rerun the exact benchmark on `ProjectsPage.razor` and `PageHeader.razor`

Success criteria:

- simple text edit becomes visible in about 14s plus only small management overhead
- `Hot reload succeeded` and browser-visible change line up again

## Phase 2

Goal: repair confirmation semantics.

- add a runtime hot-reload generation token
- return it from `/_dev/runtime`
- change `RevisionConfirmed` so it is not satisfied by the same watch iteration plus `pending=false`
- add a new lightweight wait for `WatchReportedApplied` if callers still want log-level completion

Success criteria:

- MCP cannot report a confirmed hot reload when the runtime generation has not changed

## Phase 3

Goal: speed up managed builds without losing log clarity.

- introduce two managed build profiles:
  - `InnerLoopBuild`: normal outputs, `--no-restore` when safe, MSBuild server on
  - `IsolatedBuild`: current isolation-oriented behavior for special cases
- keep cleaned log summarization as a post-process
- do not force isolated artifacts output for the default warm build path

Success criteria:

- warm managed build lands close to manual `dotnet build --no-restore`

## Phase 4

Goal: avoid overlapping edit confusion.

- add a logical-app mutation queue
- reject or defer a second nearby edit while the first one is unresolved
- surface "watch still processing previous mutation" as explicit status

Success criteria:

- agents stop accidentally stacking edits or retries on one watch session

## Validation Matrix

- rerun `tools/watch_benchmark.js` for:
  - plain watch
  - new managed `SourceWatch`
  - `ProjectsPage.razor`
  - `PageHeader.razor`
- rerun `tools/run_build_benchmarks.ps1`
- rerun the live MCP simple-edit probe from `02-managed-watch-live-run.md`
- compare:
  - file update to hot reload log
  - file update to runtime generation change
  - file update to browser-visible change
  - startup to healthy
  - managed build elapsed vs manual build elapsed

## Suggested Prompts For The Implementation Agent

### Prompt 1

Implement `SourceWatch` parity with plain local `dotnet watch`. Remove `--artifacts-path` from the watch lane, keep other lanes unchanged, and re-run the benchmark on `ProjectsPage.razor` and `PageHeader.razor`.

### Prompt 2

Add a real in-process hot-reload generation token using a `MetadataUpdateHandler`, return it from `/_dev/runtime`, and rework `RevisionConfirmed` so it cannot be satisfied by stale watch iteration data.

### Prompt 3

Split the build lane into fast inner-loop builds and isolated builds. Preserve cleaned MCP summaries, but stop forcing isolated artifacts output for the default warm build path.
