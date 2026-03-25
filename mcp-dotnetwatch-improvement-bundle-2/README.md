# MCP Dotnet Watch Improvement Bundle 2

This bundle started as a root-cause analysis package and is now implemented and validated.

## Final Status

Implemented:

- `SourceWatch` no longer uses `--artifacts-path`.
- managed watch/build flows keep `DOTNET_CLI_USE_MSBUILD_SERVER=1`.
- the web app exposes a real `HotReloadGeneration` from `/_dev/runtime`.
- MCP adds `WatchReady` plus runtime-generation-aware `RevisionConfirmed`.
- startup watch logs no longer create false pending edits before the first `Waiting for changes`.
- the workspace default config now disables `DOTNET_USE_POLLING_FILE_WATCHER`.
- an isolated benchmark runner now exists so final measurements are not polluted by bridge/shadow registration churn.

## Acceptance Result

Acceptance gate:

- managed watch change should be visible in about 15 seconds or less

Final isolated benchmark results on March 25, 2026:

- `PageHeader.razor`: `7.719s` watch-confirmed, `8.145s` browser-visible
- `ProjectsPage.razor`: `11.312s` watch-confirmed, `11.703s` browser-visible

The hot-reload inner loop is back under the target on both benchmark files.

## Validation

- `dotnet test tests\CanDoItAll.Mcp.DotNetWatch.Tests\CanDoItAll.Mcp.DotNetWatch.Tests.csproj --nologo`
  - passed `35/35`
- `dotnet test tests\CanDoItAll.Mcp.DotNetWatch.IntegrationTests\CanDoItAll.Mcp.DotNetWatch.IntegrationTests.csproj --nologo --filter "FullyQualifiedName~AppStart_WaitHealthy_Stop_WorksAgainstCurrentRepo|FullyQualifiedName~SolutionBuild_StopAndResume_WorksAgainstCurrentRepo|FullyQualifiedName~TestsRun_StopAndResume_WorksAgainstCurrentRepo"`
  - passed `3/3`
- isolated managed watch benchmark runner:
  - `tools/run_isolated_watch_benchmarks.ps1`
  - summary output: `artifacts/final-watch-benchmark-summary.json`

## Main Remaining Issues

- the default bridge/shadow backend can still overwrite the shared backend registration file after failures or restarts and bring an older shadow-host binary back into play
- cold watch startup in this environment still measured `59-82s`, which is better than the earlier broken flow but still slower than the user's local `~30s` startup baseline
- the full integration test project did not complete within a `15m` timeout, so final validation used the focused app-start/app-wait/build-resume/test-resume slice

## Bundle Map

- `01-watch-benchmark-matrix.md`
- `02-managed-watch-live-run.md`
- `03-build-benchmark-findings.md`
- `04-code-path-root-causes.md`
- `05-architecture-options.md`
- `06-followup-agent-plan.md`
- `07-execution-subbundles.md`
- `08-implementation-results.md`
- `subbundles/01-sourcewatch-parity-checklist.md`
- `subbundles/02-runtime-confirmation-checklist.md`
- `subbundles/03-managed-build-fastpath-checklist.md`
- `subbundles/04-validation-checklist.md`

## Key Artifacts

- `artifacts/final-watch-benchmark-summary.json`
- `artifacts/managed-mcp-pageheader-fullflow.json`
- `artifacts/managed-mcp-projects-page-fullflow.json`
- `artifacts/managed-mcp-pageheader-no-polling.json`
- `artifacts/build-bench/summary.json`
- `artifacts/build-factor/summary.json`
- `tools/managed_mcp_watch_benchmark.js`
- `tools/run_isolated_watch_benchmarks.ps1`
- `tools/run_build_benchmarks.ps1`
