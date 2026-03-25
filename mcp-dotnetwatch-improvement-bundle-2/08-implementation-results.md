# Implementation Results

This file records what was actually changed, how it was validated, and what still blocks a cleaner architecture.

## Sub-Bundle 1: SourceWatch Parity

Implemented changes:

- removed `--artifacts-path` from `SourceWatch`
- kept `DOTNET_CLI_USE_MSBUILD_SERVER=1`
- changed the workspace defaults so `DOTNET_USE_POLLING_FILE_WATCHER` is no longer forced on

Final measured result:

- `PageHeader.razor`: `8.145s` visible in browser
- `ProjectsPage.razor`: `11.703s` visible in browser

Evidence:

- `artifacts/managed-mcp-pageheader-fullflow.json`
- `artifacts/managed-mcp-projects-page-fullflow.json`
- `artifacts/final-watch-benchmark-summary.json`

## Sub-Bundle 2: Runtime Confirmation

Implemented changes:

- added runtime hot-reload generation tracking in the web app
- surfaced the generation from `/_dev/runtime`
- parsed it in the health probe
- kept hot-reload confirmation pending until runtime generation advanced
- added `WatchReady`
- stopped treating pre-ready startup logs as implicit file edits

Result:

- `RevisionConfirmed` now tracks a real runtime-visible hot-reload generation
- browser-visible validation matched the runtime confirmation in the final isolated runs

## Sub-Bundle 3: Managed Build Fast Path

Implemented changes:

- managed build/test operations now keep MSBuild server enabled
- the fast path injects `--no-restore` only when safe
- restore-related failures retry without `--no-restore`
- cleaned summaries and resume behavior were preserved

Evidence:

- `03-build-benchmark-findings.md`
- `artifacts/build-bench/summary.json`
- `artifacts/build-factor/summary.json`

## Final Validation Run

Repeatable runner:

- `tools/run_isolated_watch_benchmarks.ps1`

Why it exists:

- the default bridge/shadow backend can re-register itself over the shared backend registration file
- that behavior polluted direct benchmark runs and occasionally reverted the live backend to an older shadow-host binary
- the isolated runner launches the current debug build on its own backend registration file and benchmarks against that isolated endpoint only

Final isolated summary:

- backend binary marker: `378CBA6D6E7E63383921C752F8E64B56115A6AF7678C2B33CC4CF367E20E219D`
- page header hot reload: `7.719s` watch-confirmed, `8.145s` browser-visible
- projects page hot reload: `11.312s` watch-confirmed, `11.703s` browser-visible

## Tests

Passed:

- `35/35` in `CanDoItAll.Mcp.DotNetWatch.Tests`
- `3/3` focused integration tests:
  - `AppStart_WaitHealthy_Stop_WorksAgainstCurrentRepo`
  - `SolutionBuild_StopAndResume_WorksAgainstCurrentRepo`
  - `TestsRun_StopAndResume_WorksAgainstCurrentRepo`

Not completed in final pass:

- the full integration test project did not finish within a `15m` timeout

## Remaining Architecture Issues

- the bridge/shadow ownership model still allows the shared backend registration file to flip to a different backend after failures or repair attempts
- that behavior can invalidate benchmark runs even when the runtime logic itself is fixed
- the next repair should separate the persistent manager process from the MCP stdio bridge so MCP calls are clients of the manager, not competing owners of the same backend lifecycle
